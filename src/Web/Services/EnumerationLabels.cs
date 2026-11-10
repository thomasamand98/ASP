namespace Horizon.Web.Services;

// libellés par code, vide en cas d'erreur (l'appelant affiche le code brut)
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
