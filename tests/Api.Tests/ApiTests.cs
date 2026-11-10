using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

// pipeline HTTP complet, sans base : routes sans accès BDD seulement
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        // base injoignable exprès, pas besoin de .env
        Environment.SetEnvironmentVariable("DATABASE_URL", "mysql://test:test@127.0.0.1:9/test");
        Environment.SetEnvironmentVariable("JWT_SECRET", "secret-de-test-secret-de-test-secret-de-test");
        Environment.SetEnvironmentVariable("LOGIN_THROTTLE_LIMIT", "3");
        Environment.SetEnvironmentVariable("CORS_ORIGIN", "");
        // une IP par test (X-Forwarded-For), donc compteurs de limitation séparés
        Environment.SetEnvironmentVariable("TRUST_PROXY", "1");
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Error(int statusCode, string message);

    private sealed record Session(string accessToken, string login);

    private static int _clients;

    private HttpClient Client()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"203.0.113.{Interlocked.Increment(ref _clients)}");
        return client;
    }

    [Fact]
    public async Task Une_route_protegee_sans_jeton_repond_401_avec_le_format_d_erreur()
    {
        var response = await Client().GetAsync("pays");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(new Error(401, "Session expirée ou invalide, veuillez vous reconnecter"), await response.Content.ReadFromJsonAsync<Error>());
    }

    [Fact]
    public async Task Une_route_inconnue_repond_404_comme_Express()
    {
        var response = await Client().PutAsync("nimporte?x=1", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(new Error(404, "Cannot PUT /nimporte?x=1"), await response.Content.ReadFromJsonAsync<Error>());
    }

    [Fact]
    public async Task La_connexion_renvoie_un_jeton_d_acces_et_pose_le_cookie_de_renouvellement()
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("auth/login", new { login = " alice ", password = "x" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<Session>())!;
        Assert.Equal("alice", session.login);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("refresh_token=", cookie);
        Assert.Contains("path=/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=28800", cookie, StringComparison.OrdinalIgnoreCase);

        var ping = new HttpRequestMessage(HttpMethod.Get, "performance/ping");
        ping.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.accessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(ping)).StatusCode);

        // cookie de refresh refusé comme jeton d'accès (audience différente)
        var refreshToken = cookie.Split(';')[0]["refresh_token=".Length..];
        var refresh = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        refresh.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        Assert.Equal("alice", (await (await client.SendAsync(refresh)).Content.ReadFromJsonAsync<Session>())!.login);

        var misuse = new HttpRequestMessage(HttpMethod.Get, "performance/ping");
        misuse.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(misuse)).StatusCode);
    }

    [Fact]
    public async Task Le_renouvellement_sans_cookie_valide_repond_401_et_efface_le_cookie()
    {
        var response = await Client().PostAsync("auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(new Error(401, "Session expirée, veuillez vous reconnecter"), await response.Content.ReadFromJsonAsync<Error>());
        Assert.Contains("refresh_token=;", Assert.Single(response.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task La_connexion_refuse_un_corps_incomplet()
    {
        var response = await Client().PostAsJsonAsync("auth/login", new { login = "alice" });
        Assert.Equal(new Error(400, "Vous devez renseigner l'identifiant et le mot de passe"), await response.Content.ReadFromJsonAsync<Error>());
        Assert.Equal("{}", await (await Client().PostAsync("auth/logout", null)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Les_en_tetes_de_securite_et_de_limitation_sont_poses()
    {
        var response = await Client().GetAsync("pays");
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("cross-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Resource-Policy")));
        Assert.Equal("300", Assert.Single(response.Headers.GetValues("X-RateLimit-Limit")));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
    }

    [Fact]
    public async Task Le_CORS_accepte_localhost_et_refuse_les_autres_origines()
    {
        static HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "pays");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization");
            return request;
        }

        var allowed = await Client().SendAsync(Preflight("http://localhost:5173"));
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal("http://localhost:5173", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Equal("600", Assert.Single(allowed.Headers.GetValues("Access-Control-Max-Age")));

        var refused = await Client().SendAsync(Preflight("https://exemple.be"));
        Assert.False(refused.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Les_tentatives_de_connexion_sont_limitees()
    {
        // LOGIN_THROTTLE_LIMIT=3, 4e tentative refusée
        var client = Client();
        Task<HttpResponseMessage> SendAsync() => client.PostAsJsonAsync("auth/login", new { login = "bob", password = "x" });

        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.OK, (await SendAsync()).StatusCode);
        var last = await SendAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
        Assert.Equal(new Error(429, "Trop de requêtes, veuillez réessayer dans quelques instants"), await last.Content.ReadFromJsonAsync<Error>());
    }
}

public class JsonConfigTests
{
    [Fact]
    public void Serialise_au_format_attendu_par_le_front()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                IDSOCIETES = 31415821754433543L,
                Nom_societe = "Liège & <Co>",
                Taux_tva = 21.500000m,
                Entier = 21.000000m,
                Total = 42,
                Date_debut = new DateTime(2026, 1, 15),
                Heure = new TimeSpan(8, 30, 0),
                Vide = (string?)null,
            },
            JsonConfig.Options);
        Assert.Equal(
            """{"IDSOCIETES":"31415821754433543","Nom_societe":"Liège & <Co>","Taux_tva":"21.5","Entier":"21","Total":42,"Date_debut":"2026-01-15T00:00:00.000Z","Heure":"1970-01-01T08:30:00.000Z","Vide":null}""",
            json);
    }

    [Theory]
    [InlineData(255L, "#ff0000")]
    [InlineData(65280L, "#00ff00")]
    [InlineData(16711680L, "#0000ff")]
    public void Convertit_les_couleurs_WinDev(long value, string expected) => Assert.Equal(expected, WindevColor.ToHex(value));

    [Fact]
    public void Une_couleur_WinDev_absente_ou_hors_bornes_vaut_null()
    {
        Assert.Null(WindevColor.ToHex(null));
        Assert.Null(WindevColor.ToHex(0));
        Assert.Null(WindevColor.ToHex(16777216));
    }
}
