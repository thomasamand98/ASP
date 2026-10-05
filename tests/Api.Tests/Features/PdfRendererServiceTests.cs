using Api.Common;
using Api.Features.DocumentMerge;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Features;

// Portage de backend/src/document-merge/pdf-renderer.service.spec.ts.
public class PdfRendererServiceTests
{
    private static readonly RenderInput Input = new("", "<p>Contrat</p>", "");

    // Faux Chromium : `render` décide du résultat de chaque génération,
    // `connected` indique s'il tourne encore au moment de l'échec.
    private sealed class FakeBrowser(Func<Task<byte[]>> render, bool connected = true) : IPdfBrowser
    {
        public bool IsConnected => connected;
#pragma warning disable CS0067 // jamais déclenché par ce faux navigateur
        public event Action? Disconnected;
#pragma warning restore CS0067
        public Task<byte[]> RenderAsync(RenderInput input) => render();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static Task<byte[]> Ok() => Task.FromResult<byte[]>([1, 2, 3]);

    private static Task<byte[]> Fail() => Task.FromException<byte[]>(new InvalidOperationException("Protocol error: Target closed"));

    // Lanceur qui renvoie les navigateurs fournis, dans l'ordre (le dernier
    // est réutilisé), et compte les lancements.
    private static (PdfRendererService Service, Func<int> Launches) Service(params Func<Task<IPdfBrowser>>[] launches)
    {
        var count = 0;
        var service = new PdfRendererService(NullLogger<PdfRendererService>.Instance, () => launches[Math.Min(count++, launches.Length - 1)]());
        return (service, () => count);
    }

    private static Func<Task<IPdfBrowser>> Browser(Func<Task<byte[]>> render, bool connected = true) =>
        () => Task.FromResult<IPdfBrowser>(new FakeBrowser(render, connected));

    [Fact]
    public async Task Renvoie_le_PDF_et_reutilise_la_meme_instance_Chromium()
    {
        var (service, launches) = Service(Browser(Ok));
        Assert.Equal([1, 2, 3], await service.RenderPdfAsync(Input));
        await service.RenderPdfAsync(Input);
        Assert.Equal(1, launches());
    }

    [Fact]
    public async Task Renvoie_503_sans_detail_interne_quand_Chromium_ne_demarre_pas_puis_relance_a_la_demande_suivante()
    {
        var (service, launches) = Service(
            () => Task.FromException<IPdfBrowser>(new InvalidOperationException("Failed to launch the browser process")),
            Browser(Ok));
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.RenderPdfAsync(Input));
        Assert.DoesNotContain("launch", error.Message);
        Assert.Equal([1, 2, 3], await service.RenderPdfAsync(Input));
        Assert.Equal(2, launches());
    }

    [Fact]
    public async Task Relance_Chromium_et_retente_une_fois_quand_l_instance_s_est_arretee()
    {
        var (service, launches) = Service(Browser(Fail, connected: false), Browser(Ok));
        Assert.Equal([1, 2, 3], await service.RenderPdfAsync(Input));
        Assert.Equal(2, launches());
    }

    [Fact]
    public async Task Renvoie_503_sans_retenter_quand_le_rendu_echoue_sur_une_instance_vivante()
    {
        var (service, launches) = Service(Browser(Fail));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.RenderPdfAsync(Input));
        Assert.Equal(1, launches());
    }
}
