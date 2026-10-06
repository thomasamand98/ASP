using System.Text.RegularExpressions;
using Api.Common;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Api.Features.DocumentMerge;

public record RenderInput(string HeaderHtml, string BodyHtml, string FooterHtml);

// Navigateur capable de produire un PDF (Chromium en production, un faux
// dans les tests).
public interface IPdfBrowser : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action? Disconnected;
    Task<byte[]> RenderAsync(RenderInput input);
}

// Rendu PDF d'un document fusionné, via un Chromium sans interface
// (PuppeteerSharp, équivalent de puppeteer côté NestJS). Une seule instance
// de Chromium est lancée à la première demande puis réutilisée ; si elle
// s'arrête, elle est relancée et le rendu retenté une fois. Tout échec est
// renvoyé en 503, sans détail interne.
public partial class PdfRendererService(ILogger<PdfRendererService> logger, Func<Task<IPdfBrowser>>? launcher = null) : IAsyncDisposable
{
    public const string UnavailableMessage = "Génération du PDF impossible pour le moment, réessayez dans quelques instants.";

    private readonly Func<Task<IPdfBrowser>> _launch = launcher ?? ChromiumBrowser.LaunchAsync;
    private readonly Lock _gate = new();
    private Task<IPdfBrowser>? _browserTask;

    private Task<IPdfBrowser> GetBrowser()
    {
        lock (_gate)
        {
            if (_browserTask is not null) return _browserTask;
            var holder = new TaskCompletionSource<IPdfBrowser>(TaskCreationOptions.RunContinuationsAsynchronously);
            _browserTask = holder.Task;
            _ = LaunchAsync(holder);
            return holder.Task;
        }
    }

    private async Task LaunchAsync(TaskCompletionSource<IPdfBrowser> holder)
    {
        try
        {
            var browser = await _launch();
            // Chromium arrêté : on l'oublie, la demande suivante le relancera.
            browser.Disconnected += () => Forget(holder.Task);
            holder.SetResult(browser);
        }
        catch (Exception error)
        {
            // Lancement raté : idem.
            Forget(holder.Task);
            holder.SetException(error);
        }
    }

    private void Forget(Task<IPdfBrowser> launching)
    {
        lock (_gate)
        {
            if (_browserTask == launching) _browserTask = null;
        }
    }

    public async Task<byte[]> RenderPdfAsync(RenderInput input)
    {
        for (var attempt = 1; ; attempt++)
        {
            var launched = GetBrowser();
            IPdfBrowser? browser = null;
            try
            {
                browser = await launched;
                return await browser.RenderAsync(input);
            }
            catch (Exception error)
            {
                var browserLost = browser is not null && !browser.IsConnected;
                if (browserLost) Forget(launched);
                if (browserLost && attempt == 1)
                {
                    logger.LogWarning("Chromium arrêté, nouvelle tentative : {Detail}", DescribeError(error));
                    continue;
                }
                logger.LogError("Génération du PDF impossible : {Detail}", DescribeError(error));
                throw new ServiceUnavailableException(UnavailableMessage);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task<IPdfBrowser>? task;
        lock (_gate) task = _browserTask;
        if (task is null) return;
        try
        {
            await (await task).DisposeAsync();
        }
        catch
        {
            // Arrêt de l'application : rien à faire de plus.
        }
        GC.SuppressFinalize(this);
    }

    private static string DescribeError(Exception error) => Newlines().Replace(error.ToString(), " | ");

    [GeneratedRegex(@"\s*\n\s*")]
    private static partial Regex Newlines();
}

// Chromium piloté par PuppeteerSharp.
public sealed class ChromiumBrowser(IBrowser browser) : IPdfBrowser
{
    // Feuille de style du corps du document : reprend les classes produites
    // par l'éditeur du front (frontend/src/features/documents/).
    private const string BodyStylesheet = """

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

        """;

    // En-tête et pied de page : rendus par Chromium dans la marge, avec leur
    // propre feuille de style (celle du corps ne s'y applique pas).
    private const string ChromeStylesheet = """

          * { box-sizing: border-box; font: 8px/1.4 'Segoe UI', Arial, sans-serif; color: #444; }
          p { margin: 0 0 2px; }
          strong { font-weight: 700; }
          .dte-header-row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
          .dte-address-block { min-width: 0; }
          .dte-table { border-collapse: collapse; font-size: 8px; }
          .dte-table th, .dte-table td { border: 0.5px solid #ccc; padding: 2px 4px; }
          .dte-merged-image { max-height: 18mm; max-width: 45mm; object-fit: contain; }

        """;

    public bool IsConnected => browser.IsConnected;

    public event Action? Disconnected;

    public static async Task<IPdfBrowser> LaunchAsync()
    {
        var launched = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            // --disable-dev-shm-usage : Docker plafonne /dev/shm à 64 Mo, ce
            // qui fait tomber le rendu sur un gros lot (impression de tous les
            // contrats) ; Chromium passe alors par /tmp.
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"],
            ExecutablePath = await FindChromiumAsync(),
        });
        var wrapper = new ChromiumBrowser(launched);
        launched.Disconnected += (_, _) => wrapper.Disconnected?.Invoke();
        return wrapper;
    }

    // Chromium à utiliser : PUPPETEER_EXECUTABLE_PATH (image Docker), sinon
    // celui déjà téléchargé par puppeteer (npm) sur ce poste, sinon un
    // téléchargement par PuppeteerSharp à la première génération.
    private static async Task<string> FindChromiumAsync()
    {
        var configured = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var npmCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "puppeteer", "chrome");
        if (Directory.Exists(npmCache))
        {
            var executable = Directory.EnumerateFiles(npmCache, OperatingSystem.IsWindows() ? "chrome.exe" : "chrome", SearchOption.AllDirectories)
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (executable is not null) return executable;
        }

        var installed = await new BrowserFetcher().DownloadAsync();
        return installed.GetExecutablePath();
    }

    public async Task<byte[]> RenderAsync(RenderInput input)
    {
        var page = await browser.NewPageAsync();
        try
        {
            // Le HTML vient d'un modèle saisi par un utilisateur : aucun script
            // n'est exécuté et aucune ressource externe n'est chargée (seules
            // les images intégrées en data: passent).
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetRequestInterceptionAsync(true);
            page.Request += (_, e) =>
            {
                var url = e.Request.Url;
                _ = url.StartsWith("data:", StringComparison.Ordinal) || url == "about:blank"
                    ? e.Request.ContinueAsync()
                    : e.Request.AbortAsync(RequestAbortErrorCode.BlockedByClient);
            };
            var document = $"<!doctype html><html><head><meta charset=\"utf-8\"><style>{BodyStylesheet}</style></head><body>{input.BodyHtml}</body></html>";
            await page.SetContentAsync(document, new SetContentOptions { WaitUntil = [WaitUntilNavigation.Load] });
            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = input.HeaderHtml.Length > 0 ? "32mm" : "18mm",
                    Bottom = input.FooterHtml.Length > 0 ? "20mm" : "16mm",
                    Left = "16mm",
                    Right = "16mm",
                },
                DisplayHeaderFooter = true,
                HeaderTemplate = input.HeaderHtml.Length > 0 ? BuildChromeFragment(input.HeaderHtml) : "<span></span>",
                FooterTemplate = input.FooterHtml.Length > 0 ? BuildChromeFragment(input.FooterHtml) : "<span></span>",
            });
        }
        finally
        {
            try
            {
                await page.CloseAsync();
            }
            catch
            {
                // Page déjà fermée (Chromium arrêté).
            }
        }
    }

    private static string BuildChromeFragment(string html) =>
        $"<style>{ChromeStylesheet}</style><div style=\"width:100%;margin:0;padding:0 16mm;box-sizing:border-box;\">{html}</div>";

    public async ValueTask DisposeAsync() => await browser.CloseAsync();
}

// Point d'entrée du moteur de fusion : assemble le modèle
// (DocumentTemplateService) et un contexte de données déjà construit par
// l'appelant (voir ContratService.GetContratMergeContextAsync) pour produire
// un PDF. Ne connaît rien du contrat, de la facture ou de tout autre type
// d'enregistrement.
//
// Un modèle a trois zones indépendantes : en-tête et pied de page (répétés
// sur chaque page imprimée) et le contenu principal (affiché une fois). Les
// trois sont résolues avec exactement le même contexte.
public class DocumentMergeService(DocumentTemplates.DocumentTemplateService templates, PdfRendererService renderer)
{
    public async Task<byte[]> RenderPdfAsync(long templateId, object? context, CancellationToken ct)
    {
        var content = await templates.GetDocumentTemplateContentAsync(templateId, ct);
        return await renderer.RenderPdfAsync(new RenderInput(
            string.IsNullOrEmpty(content.Contenu_entete) ? "" : MergeHtml.Resolve(content.Contenu_entete, context),
            MergeHtml.Resolve(content.Contenu_html, context),
            string.IsNullOrEmpty(content.Contenu_pied) ? "" : MergeHtml.Resolve(content.Contenu_pied, context)));
    }
}
