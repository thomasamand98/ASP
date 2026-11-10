namespace Horizon.Web.Services;

// lu depuis wwwroot/appsettings.json
public sealed class AppConfig
{
    // vide = hôte de la page sur le port 3000 (dev local)
    public string ApiUrl { get; init; } = "";

    // false en attendant la vraie auth : session automatique
    public bool LoginScreen { get; init; }

    // schéma de la page, sinon contenu mixte bloqué en HTTPS
    public static string ResolveApiUrl(string configured, Uri pageUri) =>
        string.IsNullOrWhiteSpace(configured) ? $"{pageUri.Scheme}://{pageUri.Host}:3000" : configured.TrimEnd('/');
}
