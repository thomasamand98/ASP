namespace Horizon.Web.Services;

// même clé que le script de index.html, sinon flash au chargement
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

    public ValueTask ApplyAsync() => browser.ApplyThemeAsync(Preference);
}
