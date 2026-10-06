using System.Net;
using System.Text;
using Bunit;
using Horizon.Web.Auth;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Horizon.Web.Tests.Components;

public class AuthGateTests : BunitContext
{
    // API simulée : /auth/refresh répond 401 (pas de cookie), /auth/login
    // ouvre une session.
    private sealed class FakeApi : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        public HttpStatusCode RefreshStatus { get; set; } = HttpStatusCode.Unauthorized;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add(path);
            var session = new StringContent("""{"accessToken":"jeton","login":"utilisateur"}""", Encoding.UTF8, "application/json");
            return Task.FromResult(path switch
            {
                "/auth/refresh" when RefreshStatus == HttpStatusCode.OK => new HttpResponseMessage(HttpStatusCode.OK) { Content = session },
                "/auth/refresh" => new HttpResponseMessage(RefreshStatus) { Content = new StringContent("""{"message":"Session expirée"}""") },
                "/auth/login" => new HttpResponseMessage(HttpStatusCode.Created) { Content = session },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }
    }

    private readonly FakeApi _api = new();

    private IRenderedComponent<AuthGate> Rendre(bool loginScreen)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new AppConfig { LoginScreen = loginScreen });
        Services.AddSingleton(new HttpClient(_api) { BaseAddress = new Uri("http://api.test/") });
        Services.AddSingleton<AuthSession>();
        return Render<AuthGate>(p => p.AddChildContent("<p id='app'>Application</p>"));
    }

    [Fact]
    public void Sans_ecran_de_connexion_une_session_automatique_est_ouverte()
    {
        var gate = Rendre(loginScreen: false);
        gate.WaitForElement("#app");
        Assert.Equal(["/auth/refresh", "/auth/login"], _api.Calls);
        Assert.Equal("jeton", Services.GetRequiredService<AuthSession>().AccessToken);
    }

    [Fact]
    public void Avec_ecran_de_connexion_on_demande_les_identifiants()
    {
        var gate = Rendre(loginScreen: true);
        gate.WaitForElement("input[type=password]");
        Assert.Empty(gate.FindAll("#app"));
        Assert.Equal(["/auth/refresh"], _api.Calls);
    }

    [Fact]
    public void Une_session_existante_est_reprise_sans_connexion()
    {
        _api.RefreshStatus = HttpStatusCode.OK;
        var gate = Rendre(loginScreen: true);
        gate.WaitForElement("#app");
        Assert.Equal(["/auth/refresh"], _api.Calls);
    }

    [Fact]
    public void La_connexion_par_formulaire_affiche_l_application()
    {
        var gate = Rendre(loginScreen: true);
        gate.WaitForElement("input[type=password]");
        gate.FindAll("input")[0].Input("thomas");
        gate.Find("input[type=password]").Input("secret");
        gate.Find("form").Submit();
        gate.WaitForElement("#app");
        Assert.Equal(["/auth/refresh", "/auth/login"], _api.Calls);
    }
}
