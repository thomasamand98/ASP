namespace Horizon.Web.Services;

// Thème clair/sombre de toute l'application. Le thème appliqué est porté par
// l'attribut `data-theme` de <html>, que css/tokens.css lit pour choisir ses
// jetons de couleur. « system » suit le réglage du système d'exploitation, y
// compris quand il change pendant que l'application est ouverte.
// Le script en tête de index.html applique le même choix avant le premier
// rendu (pas de flash de l'autre thème au chargement) : garder la même clé.
public sealed class ThemeService(Browser browser)
{
    private const string StorageKey = "theme";

    public string Preference { get; private set; } = "system";

    public void Load()
    {
        var stored = browser.GetItem(StorageKey);
        Preference = stored is "light" or "dark" ? stored : "system";
    }

    public async Task SetAsync(string preference)
    {
        Preference = preference;
        await browser.ApplyThemeAsync(preference);
        if (preference == "system") await browser.RemoveItemAsync(StorageKey);
        else await browser.SetItemAsync(StorageKey, preference);
    }

    // Suivi du réglage système au démarrage si aucun choix n'est mémorisé.
    public ValueTask ApplyAsync() => browser.ApplyThemeAsync(Preference);
}
