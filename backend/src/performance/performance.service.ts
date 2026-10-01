// ===== MODEL (logique métier) =====
// Traitements longs pour tester la réactivité de l'application
// (frontend/src/features/performance/).
//
// Traitement fictif, deux façons d'occuper le serveur :
//   - « attente » : attente asynchrone (comme une requête SQL ou un appel
//     d'API lent). Node reste libre de servir les autres requêtes.
//   - « cpu » : calcul synchrone. Bloque la boucle d'événements de Node :
//     plus aucune requête n'est servie, pour aucun utilisateur, tant que le
//     calcul n'est pas terminé.
//
// Traitement réel : impression de tous les contrats en PDF (voir
// imprimerTousLesContrats), enregistrés sur le serveur.
//
// Test de transfert : réponse JSON d'une taille choisie, avec ou sans
// compression (voir preparerTransfert).
import { ConflictException, Injectable, Logger } from '@nestjs/common';
import { mkdir, readdir, rm, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { setTimeout as attendre } from 'node:timers/promises';
import { promisify } from 'node:util';
import { gzip as gzipCallback } from 'node:zlib';
import { ContratService } from '../contrat/contrat.service.js';
import { DocumentMergeService } from '../document-merge/document-merge.service.js';
import { DocumentTemplateService } from '../document-template/document-template.service.js';
import { PrismaService } from '../prisma/prisma.service.js';

export type ModeTraitement = 'attente' | 'cpu';

export type ResultatTraitement = {
  mode: ModeTraitement;
  dureeDemandeeMs: number;
  dureeReelleMs: number;
  debut: string;
  fin: string;
  iterations?: number;
};

// Sous-répertoire où sont enregistrés les PDF, sous PDF_STORAGE_DIR (voir
// .env.example ; par défaut « storage », relatif au dossier de lancement du
// backend). Vidé de ses PDF de contrats puis réécrit à chaque impression :
// « contrat-<id>.pdf », puis « contrat-<id>-ex2.pdf », « -ex3 »... pour les
// exemplaires suivants.
const SOUS_REPERTOIRE_CONTRATS = 'contrats';
const FICHIER_CONTRAT = /^contrat-\d+(-ex\d+)?\.pdf$/;

// dureeMs/octets : cumul des exemplaires imprimés de ce contrat.
export type ImpressionContrat = { id: string; numero: string | null; exemplaires: number; dureeMs: number; octets: number };
export type EchecImpression = { id: string; numero: string | null; message: string };

export type ResultatImpression = {
  dossier: string;
  // Nombre de contrats, et d'exemplaires demandés pour chacun.
  total: number;
  exemplaires: number;
  // Nombre de PDF réellement enregistrés (au plus total × exemplaires).
  impressions: number;
  dureeTotaleMs: number;
  // Par PDF, sur ceux imprimés avec succès ; null si aucun.
  dureeMoyenneMs: number | null;
  dureeMinMs: number | null;
  dureeMaxMs: number | null;
  octetsTotal: number;
  debut: string;
  fin: string;
  contrats: ImpressionContrat[];
  echecs: EchecImpression[];
};

export type AvancementImpression = { faits: number; total: number };

// corps : octets à envoyer tels quels (JSON, compressé ou non).
export type Transfert = { corps: Buffer; octetsJson: number; generationMs: number; jsonMs: number; compressionMs: number };

const gzip = promisify(gzipCallback);

@Injectable()
export class PerformanceService {
  private readonly logger = new Logger('Performance');
  // Renseigné pendant une impression en cours : renvoyé par le ping pour
  // afficher l'avancement côté front, et empêche d'en lancer une deuxième.
  private avancementImpression: AvancementImpression | null = null;

  constructor(
    private readonly prisma: PrismaService,
    private readonly contratService: ContratService,
    private readonly documentTemplateService: DocumentTemplateService,
    private readonly documentMergeService: DocumentMergeService,
  ) {}

  getAvancementImpression(): AvancementImpression | null {
    return this.avancementImpression;
  }

  async lancerTraitementLong(mode: ModeTraitement, dureeMs: number): Promise<ResultatTraitement> {
    const debut = new Date();
    let iterations: number | undefined;

    if (mode === 'attente') {
      await attendre(dureeMs);
    } else {
      iterations = calculerPendant(dureeMs);
    }

    const fin = new Date();
    return {
      mode,
      dureeDemandeeMs: dureeMs,
      dureeReelleMs: fin.getTime() - debut.getTime(),
      debut: debut.toISOString(),
      fin: fin.toISOString(),
      ...(iterations !== undefined && { iterations }),
    };
  }

  // Prépare une réponse JSON d'environ `tailleKo` Ko, compressée (gzip) ou
  // non, et chronomètre chaque étape côté serveur. La génération des données
  // n'est pas une étape du transfert : elle est mesurée à part pour pouvoir
  // être retirée du temps total constaté par le navigateur.
  async preparerTransfert(tailleKo: number, compression: boolean): Promise<Transfert> {
    let depart = performance.now();
    const donnees = { lignes: genererLignes(tailleKo * 1024) };
    const generationMs = performance.now() - depart;

    depart = performance.now();
    const json = Buffer.from(JSON.stringify(donnees));
    const jsonMs = performance.now() - depart;

    depart = performance.now();
    const corps = compression ? await gzip(json) : json;
    const compressionMs = compression ? performance.now() - depart : 0;

    return { corps, octetsJson: json.length, generationMs, jsonMs, compressionMs };
  }

  // Imprime tous les contrats avec le modèle choisi, en `exemplaires`
  // exemplaires chacun, l'un après l'autre (la durée de chacun n'est ainsi
  // pas faussée par les autres), et enregistre chaque PDF sur le serveur.
  // Chaque exemplaire est une impression complète (lecture en base, fusion,
  // rendu), pas une copie du fichier : c'est la charge qui est mesurée. Un
  // contrat en échec n'arrête pas l'impression des suivants : il est listé
  // dans `echecs`.
  async imprimerTousLesContrats(templateId: bigint, exemplaires: number): Promise<ResultatImpression> {
    if (this.avancementImpression) throw new ConflictException('Une impression de tous les contrats est déjà en cours.');
    // Modèle inexistant : 404 tout de suite, plutôt qu'un échec par contrat.
    await this.documentTemplateService.getDocumentTemplateContent(templateId);

    const contrats = await this.prisma.contrat.findMany({
      select: { IDCONTRATS: true, Num_contrat: true },
      orderBy: { IDCONTRATS: 'asc' },
    });
    const aImprimer = contrats.length * exemplaires;
    this.avancementImpression = { faits: 0, total: aImprimer };
    const debut = new Date();
    const imprimes: ImpressionContrat[] = [];
    const echecs: EchecImpression[] = [];
    // Une durée par PDF enregistré.
    const durees: number[] = [];
    let faits = 0;

    try {
      const dossier = resolve(process.env.PDF_STORAGE_DIR?.trim() || 'storage', SOUS_REPERTOIRE_CONTRATS);
      await mkdir(dossier, { recursive: true });
      // Le dossier ne doit contenir que les PDF de cette impression (sinon
      // les exemplaires d'un lancement précédent, plus nombreux, y resteraient).
      for (const fichier of await readdir(dossier)) {
        if (FICHIER_CONTRAT.test(fichier)) await rm(join(dossier, fichier));
      }

      for (const contrat of contrats) {
        const id = String(contrat.IDCONTRATS);
        const imprime: ImpressionContrat = { id, numero: contrat.Num_contrat, exemplaires: 0, dureeMs: 0, octets: 0 };
        try {
          for (let exemplaire = 1; exemplaire <= exemplaires; exemplaire++) {
            const depart = performance.now();
            const context = await this.contratService.getContratMergeContext(contrat.IDCONTRATS);
            const pdf = await this.documentMergeService.renderPdf(templateId, context);
            await writeFile(join(dossier, `contrat-${id}${exemplaire > 1 ? `-ex${exemplaire}` : ''}.pdf`), pdf);
            const dureeMs = Math.round(performance.now() - depart);
            durees.push(dureeMs);
            imprime.exemplaires++;
            imprime.dureeMs += dureeMs;
            imprime.octets += pdf.length;
            this.avancementImpression = { faits: ++faits, total: aImprimer };
          }
        } catch (error) {
          // Les exemplaires restants de ce contrat échoueraient de la même
          // façon : ils sont abandonnés, mais comptés dans l'avancement.
          const message = error instanceof Error ? error.message : String(error);
          this.logger.warn(`Impression du contrat ${id} impossible : ${message.replace(/\s*\n\s*/g, ' | ')}`);
          echecs.push({ id, numero: contrat.Num_contrat, message });
          faits += exemplaires - imprime.exemplaires;
          this.avancementImpression = { faits, total: aImprimer };
        }
        if (imprime.exemplaires > 0) imprimes.push(imprime);
      }

      const fin = new Date();
      return {
        dossier,
        total: contrats.length,
        exemplaires,
        impressions: durees.length,
        dureeTotaleMs: fin.getTime() - debut.getTime(),
        dureeMoyenneMs: durees.length ? Math.round(durees.reduce((somme, d) => somme + d, 0) / durees.length) : null,
        dureeMinMs: durees.length ? Math.min(...durees) : null,
        dureeMaxMs: durees.length ? Math.max(...durees) : null,
        octetsTotal: imprimes.reduce((somme, c) => somme + c.octets, 0),
        debut: debut.toISOString(),
        fin: fin.toISOString(),
        contrats: imprimes,
        echecs,
      };
    } finally {
      this.avancementImpression = null;
    }
  }
}

// Lignes fictives ressemblant à une liste de l'application (libellés qui se
// répètent, identifiants et montants qui varient) : leur JSON se compresse
// comme une vraie réponse, ni trop bien (texte identique) ni trop mal
// (données aléatoires). Pseudo-aléatoire à graine fixe : même contenu d'un
// appel à l'autre, pour comparer les mesures.
function genererLignes(octetsVoulus: number): LigneTransfert[] {
  let graine = 42;
  const hasard = (max: number) => {
    graine = (graine * 1_103_515_245 + 12_345) % 2_147_483_648;
    return graine % max;
  };
  const lignes: LigneTransfert[] = [];
  // 12 : enveloppe {"lignes":[]} ; +1 par ligne : la virgule.
  let octets = 12;
  while (octets < octetsVoulus) {
    const ligne: LigneTransfert = {
      id: lignes.length + 1,
      societe: `${NOMS[hasard(NOMS.length)]} ${FORMES[hasard(FORMES.length)]}`,
      ville: VILLES[hasard(VILLES.length)],
      statut: STATUTS[hasard(STATUTS.length)],
      numero: `C-${2020 + hasard(7)}-${String(hasard(100_000)).padStart(5, '0')}`,
      montant: hasard(1_000_000) / 100,
      date: new Date(Date.UTC(2020 + hasard(7), hasard(12), 1 + hasard(28))).toISOString(),
      archive: hasard(10) === 0,
    };
    lignes.push(ligne);
    octets += Buffer.byteLength(JSON.stringify(ligne)) + 1;
  }
  return lignes;
}

type LigneTransfert = { id: number; societe: string; ville: string; statut: string; numero: string; montant: number; date: string; archive: boolean };

const NOMS = ['Transports Dubois', 'Logistique Lambert', 'Martin Frères', 'Euro Cargo', 'Benelux Express', 'Ardenne Fret', 'Meuse Transit', 'Nord Vrac'];
const FORMES = ['SA', 'SRL', 'SPRL', 'NV', 'BV'];
const VILLES = ['Liège', 'Namur', 'Bruxelles', 'Charleroi', 'Anvers', 'Gand', 'Mons', 'Arlon', 'Luxembourg', 'Lille'];
const STATUTS = ['En cours', 'Planifié', 'Terminé', 'Facturé', 'Annulé'];

// Boucle de calcul volontairement synchrone : ne rend jamais la main à Node
// avant la fin de la durée demandée.
function calculerPendant(dureeMs: number): number {
  const finPrevue = Date.now() + dureeMs;
  let iterations = 0;
  let accumulateur = 0;
  while (Date.now() < finPrevue) {
    for (let i = 0; i < 10_000; i++) accumulateur += Math.sqrt(i) * Math.sin(i);
    iterations++;
  }
  // Utilise le résultat pour que le calcul ne soit pas éliminé par l'optimiseur.
  return Number.isFinite(accumulateur) ? iterations : 0;
}
