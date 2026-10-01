// ===== MODEL (rendu) =====
// Transforme le HTML déjà fusionné (voir merge-html.ts) en PDF, via une
// instance Chromium headless (Puppeteer) : le rendu réutilise le vrai moteur
// de mise en page HTML/CSS du navigateur (sauts de page, tableaux qui
// continuent sur la page suivante...), plutôt qu'une lib JS qui rastérise ou
// approxime le rendu. L'instance Chromium est démarrée une seule fois et
// réutilisée entre les requêtes (son démarrage prend ~1s, trop lent pour le
// refaire à chaque génération de PDF), et fermée proprement à l'arrêt de
// l'application.
import { Injectable, Logger, OnModuleDestroy, ServiceUnavailableException } from '@nestjs/common';
import puppeteer, { type Browser } from 'puppeteer';

// Habillage du corps principal : reprend les classes posées par l'éditeur
// (voir frontend/src/features/documents/DocumentTemplateEditor.css) pour que
// la mise en page construite à l'écran (tableaux, en-têtes...) se retrouve à
// l'identique dans le PDF — sans le chrome propre à l'édition (chips,
// bordures pointillées des blocs répétables), déjà retiré par resolveMergeHtml.
//
// .dte-repeat-subcell / .dte-table-nested : ligne et sous-tableau des
// prestations supplémentaires, imbriqués dans la ligne de leur prestation
// (voir frontend/src/features/documents/starterTemplate.ts) — mis en retrait
// et allégés visuellement pour bien montrer qu'ils s'y rattachent.
const BODY_STYLESHEET = `
  * { box-sizing: border-box; }
  body {
    margin: 0;
    font: 11pt/1.5 'Segoe UI', Arial, sans-serif;
    color: #1a1a1a;
  }
  p { margin: 0 0 0.6em; }
  h2, h3 { margin: 0.9em 0 0.4em; }
  h2 { font-size: 15pt; }
  h3 { font-size: 13pt; }
  strong { font-weight: 700; }
  .dte-header-row { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
  .dte-address-block { min-width: 12rem; }
  .dte-signature-row { margin-top: 3rem; }
  .dte-muted { color: #555; font-size: 0.85em; }
  .dte-table { width: 100%; border-collapse: collapse; font-size: 0.92em; margin: 0.3rem 0 0.8rem; }
  .dte-table th, .dte-table td { border: 1px solid #ccc; padding: 0.35rem 0.5rem; text-align: left; vertical-align: top; }
  .dte-table th { background: #f2f2f2; font-weight: 700; text-transform: uppercase; font-size: 0.75em; letter-spacing: 0.03em; }
  .dte-repeat-subcell { border-left: none; border-right: none; background: #fafafa; padding: 0.3rem 0.5rem 0.5rem 2rem; }
  .dte-table-nested { width: auto; min-width: 55%; margin: 0.1rem 0 0; border-left: 2px solid #ccc; }
  .dte-table-nested th, .dte-table-nested td { font-size: 0.85em; padding: 0.25rem 0.5rem; border-color: #ddd; }
  .dte-table-nested th { background: #f0f0f0; }
  .dte-page-break { break-after: page; }
  .dte-page-break span { display: none; }
  .dte-merged-image { max-height: 22mm; max-width: 60mm; object-fit: contain; }
`;

// Sous-ensemble de BODY_STYLESHEET pour l'en-tête/pied de page : Chromium les
// rend dans un cadre isolé, sans accès au <style> de la page principale — un
// <style> propre à chaque fragment (voir buildChromeFragment) est donc
// requis, avec des tailles adaptées à leur hauteur réduite (contrainte par
// les marges haute/basse passées à page.pdf()). `.pageNumber`/`.totalPages`
// sont des classes spéciales remplies par Puppeteer lui-même si le modèle
// les contient (voir starterTemplate.ts, pied de page). Pas de sélecteur
// `body` : ce fragment n'en a pas, voir le <div> englobant ci-dessous.
const CHROME_STYLESHEET = `
  * { box-sizing: border-box; font: 8px/1.4 'Segoe UI', Arial, sans-serif; color: #444; }
  p { margin: 0 0 2px; }
  strong { font-weight: 700; }
  .dte-header-row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
  .dte-address-block { min-width: 0; }
  .dte-table { border-collapse: collapse; font-size: 8px; }
  .dte-table th, .dte-table td { border: 0.5px solid #ccc; padding: 2px 4px; }
  .dte-merged-image { max-height: 18mm; max-width: 45mm; object-fit: contain; }
`;

// headerTemplate/footerTemplate attendent un fragment HTML (pas un document
// complet) — un <style> au début du fragment s'applique bien à son propre
// contenu, mais un <html>/<head>/<body> englobant ne serait pas interprété
// comme prévu par Puppeteer.
function buildChromeFragment(html: string): string {
  return `<style>${CHROME_STYLESHEET}</style><div style="width:100%;margin:0;padding:0 16mm;box-sizing:border-box;">${html}</div>`;
}

export type RenderInput = {
  headerHtml: string;
  bodyHtml: string;
  footerHtml: string;
};

// Message renvoyé au client quand Chromium ne peut pas produire le PDF : la
// cause réelle (lancement impossible, navigateur arrêté, délai dépassé...)
// est journalisée côté serveur, jamais exposée.
const PDF_UNAVAILABLE_MESSAGE = 'Génération du PDF impossible pour le moment, réessayez dans quelques instants.';

// Sur une seule ligne, comme AllExceptionsFilter (une entrée Cloud Logging
// par ligne).
function describeError(error: unknown): string {
  const detail = error instanceof Error ? (error.stack ?? error.message) : String(error);
  return detail.replace(/\s*\n\s*/g, ' | ');
}

@Injectable()
export class PdfRendererService implements OnModuleDestroy {
  private readonly logger = new Logger('PdfRenderer');
  private browserPromise: Promise<Browser> | null = null;

  // L'instance est oubliée si son lancement échoue ou si Chromium s'arrête
  // (plantage, mise en veille du poste, processus tué...) : sans cela, la
  // promesse gardée en cache ferait échouer toutes les générations suivantes
  // jusqu'au redémarrage de l'API. La demande suivante relance Chromium.
  private getBrowser(): Promise<Browser> {
    if (!this.browserPromise) {
      const launching = puppeteer.launch({
        headless: true,
        // --no-sandbox : requis dans la plupart des environnements
        // conteneurisés (CI, Railway...) où l'utilisateur exécutant Node n'a
        // pas les privilèges du sandbox Chromium par défaut.
        args: ['--no-sandbox', '--disable-setuid-sandbox'],
      });
      const forget = () => {
        if (this.browserPromise === launching) this.browserPromise = null;
      };
      launching.then((browser) => browser.once('disconnected', forget), forget);
      this.browserPromise = launching;
    }
    return this.browserPromise;
  }

  // Chromium est un processus externe : son indisponibilité n'est pas un bug
  // de l'API, d'où un 503 avec un message clair plutôt qu'un 500. Si
  // l'instance s'est arrêtée entre deux demandes, la génération est retentée
  // une fois sur une instance neuve avant d'abandonner.
  async renderPdf(input: RenderInput): Promise<Buffer> {
    for (let attempt = 1; ; attempt++) {
      const launched = this.getBrowser();
      let browser: Browser | undefined;
      try {
        browser = await launched;
        return await this.renderWith(browser, input);
      } catch (error) {
        const browserLost = browser !== undefined && !browser.connected;
        // Sans attendre l'événement 'disconnected', qui peut arriver après.
        if (browserLost && this.browserPromise === launched) this.browserPromise = null;
        if (browserLost && attempt === 1) {
          this.logger.warn(`Chromium arrêté, nouvelle tentative : ${describeError(error)}`);
          continue;
        }
        this.logger.error(`Génération du PDF impossible : ${describeError(error)}`);
        throw new ServiceUnavailableException(PDF_UNAVAILABLE_MESSAGE);
      }
    }
  }

  private async renderWith(browser: Browser, { headerHtml, bodyHtml, footerHtml }: RenderInput): Promise<Buffer> {
    const page = await browser.newPage();
    try {
      // Le HTML d'un modèle est saisi par les utilisateurs : il ne doit ni
      // exécuter de script, ni faire charger quoi que ce soit au serveur
      // (sinon une <iframe>/<img> vers une adresse interne se retrouverait
      // imprimée dans le PDF). Seules les data: URI (logos) passent.
      await page.setJavaScriptEnabled(false);
      await page.setRequestInterception(true);
      page.on('request', (request) => {
        const url = request.url();
        if (url.startsWith('data:') || url === 'about:blank') void request.continue();
        else void request.abort('blockedbyclient');
      });
      const document = `<!doctype html><html><head><meta charset="utf-8"><style>${BODY_STYLESHEET}</style></head><body>${bodyHtml}</body></html>`;
      await page.setContent(document, { waitUntil: 'load' });
      const pdf = await page.pdf({
        format: 'A4',
        printBackground: true,
        // Marges généreuses : l'en-tête/pied de page sont dessinés DANS ces
        // marges (Puppeteer ne les redimensionne pas tout seul selon leur
        // contenu réel) — top plus large pour laisser la place à un logo.
        margin: { top: headerHtml ? '32mm' : '18mm', bottom: footerHtml ? '20mm' : '16mm', left: '16mm', right: '16mm' },
        displayHeaderFooter: true,
        headerTemplate: headerHtml ? buildChromeFragment(headerHtml) : '<span></span>',
        footerTemplate: footerHtml ? buildChromeFragment(footerHtml) : '<span></span>',
      });
      return Buffer.from(pdf);
    } finally {
      // Sans effet si Chromium s'est arrêté entre-temps (page déjà perdue).
      await page.close().catch(() => undefined);
    }
  }

  async onModuleDestroy() {
    if (!this.browserPromise) return;
    const browser = await this.browserPromise.catch(() => null);
    await browser?.close().catch(() => undefined);
  }
}
