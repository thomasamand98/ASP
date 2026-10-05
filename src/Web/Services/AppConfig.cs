namespace Horizon.Web.Services;

// Configuration du front, lue dans wwwroot/appsettings.json (puis
// appsettings.{Environnement}.json) au démarrage.
public sealed class AppConfig
{
    // URL de base de l'API. Vide : nom d'hôte de la page + port 3000, pour
    // fonctionner sans configuration en développement local ou sur le réseau.
    public string ApiUrl { get; init; } = "";

    // Écran de connexion. Désactivé tant que la vraie authentification n'est
    // pas en place : le front ouvre alors une session automatique (voir
    // AuthSession.LoginAutomaticallyAsync) et le système de jetons continue de
    // fonctionner normalement.
    public bool LoginScreen { get; init; }

    public static string ResolveApiUrl(string configured, Uri pageUri) =>
        string.IsNullOrWhiteSpace(configured) ? $"http://{pageUri.Host}:3000" : configured.TrimEnd('/');
}
