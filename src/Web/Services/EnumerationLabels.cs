namespace Horizon.Web.Services;

// Libellés d'une catégorie d'énumération (GET /enumerations?categorie=),
// indexés par `Valeur` — pour afficher le libellé d'un code stocké tel quel
// en base (ex. l'unité 2 d'une prestation → « Tonne », catégorie
// « unite_prestation »). Rechargés à chaque ouverture d'écran ; vide si le
// chargement échoue : l'appelant retombe alors sur le code brut.
public sealed class EnumerationLabels(ApiClient api)
{
    private sealed record Row(string? Valeur, string? Valeur_affiche);

    private sealed record Response(List<Row> enumerations);

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(string categorie)
    {
        try
        {
            var json = await api.GetAsync<Response>($"enumerations?categorie={Horizon.Web.Common.Js.EncodeUriComponent(categorie)}");
            var labels = new Dictionary<string, string>();
            foreach (var e in json.enumerations)
            {
                if (e.Valeur is not null && !string.IsNullOrEmpty(e.Valeur_affiche)) labels[e.Valeur] = e.Valeur_affiche;
            }
            return labels;
        }
        catch (ApiException)
        {
            return new Dictionary<string, string>();
        }
    }
}
