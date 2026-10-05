using System.Text.Json;
using Horizon.Shared;

namespace Horizon.Web.Services;

// Liste chargée depuis l'API (équivalent de useApiList). `endpoint` est à la
// fois le chemin HTTP (ex. "societes" → GET /societes) et la clé sous
// laquelle l'API renvoie le tableau (ex. { societes: [...], total }).
//
// Non paginée : toute la liste en un appel, Total = nombre d'éléments reçus.
// Paginée (Paginated = true) : Page, PageSize, Search (?search=, appliqué
// côté serveur sur toutes les colonnes texte) et Filters (paramètres
// supplémentaires, ex. { societeId }) sont envoyés en query string, et la
// liste est rechargée à chaque changement.
public sealed class ApiList<T>(ApiClient api, string endpoint, string? key = null)
{
    // Numéro de la dernière requête lancée : une réponse plus ancienne (ex.
    // recherche « dup » arrivée après « dupont ») est ignorée au lieu
    // d'écraser la plus récente.
    private int _lastRequest;

    public List<T> Data { get; set; } = [];
    public int Total { get; private set; }
    public bool Loading { get; private set; } = true;
    public string? Error { get; private set; }

    public bool Paginated { get; init; }
    public int Page { get; private set; } = 1;
    public int PageSize { get; private set; } = 25;
    public string Search { get; private set; } = "";
    public IReadOnlyDictionary<string, string> Filters { get; private set; } = new Dictionary<string, string>();

    // Données modifiées (chargement terminé, ligne ajoutée...) : à rafraîchir.
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    public Task SetPageAsync(int page)
    {
        Page = page;
        return RefetchAsync();
    }

    // Nouvelle taille de page ou nouvelle recherche : retour à la page 1.
    public Task SetPageSizeAsync(int pageSize)
    {
        PageSize = pageSize;
        Page = 1;
        return RefetchAsync();
    }

    public Task SetSearchAsync(string search)
    {
        Search = search;
        Page = 1;
        return RefetchAsync();
    }

    // Comme les dépendances de useApiList : un filtre identique ne relance rien.
    // resetPage : retour à la page 1 (nouveau filtre choisi par l'utilisateur).
    public Task SetFiltersAsync(IReadOnlyDictionary<string, string> filters, bool resetPage = false)
    {
        var pageChanged = resetPage && Page != 1;
        if (resetPage) Page = 1;
        if (filters.Count == Filters.Count && filters.All(f => Filters.TryGetValue(f.Key, out var v) && v == f.Value)) return pageChanged ? RefetchAsync() : Task.CompletedTask;
        Filters = filters;
        return RefetchAsync();
    }

    // Réglages initiaux (avant le premier chargement), sans requête.
    public void Configure(int page, int pageSize, string search, IReadOnlyDictionary<string, string> filters)
    {
        Page = page;
        PageSize = pageSize;
        Search = search;
        Filters = filters;
    }

    public async Task RefetchAsync()
    {
        var requestId = ++_lastRequest;
        Loading = true;
        Error = null;
        Changed?.Invoke();
        var query = "";
        if (Paginated)
        {
            var parameters = new List<KeyValuePair<string, string>>
            {
                new("page", Page.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("pageSize", PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            };
            parameters.AddRange(Filters);
            if (Search.Trim() is { Length: > 0 } search) parameters.Add(new("search", search));
            query = "?" + string.Join('&', parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        }
        try
        {
            var json = await api.GetAsync<JsonElement>($"{endpoint}{query}");
            if (requestId != _lastRequest) return;
            Data = json.GetProperty(key ?? endpoint).Deserialize<List<T>>(JsonConfig.Options) ?? [];
            Total = json.TryGetProperty("total", out var total) && total.ValueKind == JsonValueKind.Number ? total.GetInt32() : Data.Count;
        }
        catch (Exception e)
        {
            if (requestId == _lastRequest) Error = e.Message;
        }
        finally
        {
            if (requestId == _lastRequest)
            {
                Loading = false;
                Changed?.Invoke();
            }
        }
    }
}

// Création / modification / suppression pour un endpoint (équivalent de
// useApiMutation). Create/Update renvoient l'enregistrement à jour tel que la
// liste l'attend (T) ; Get renvoie la fiche détaillée (TDetail), pour les
// écrans dont la fiche contient plus de champs que la liste.
public sealed class CrudApi<TDto, T, TDetail>(ApiClient api, string endpoint)
{
    public async Task<TDetail> GetAsync(string id) => (await api.SendJsonAsync<TDetail>(HttpMethod.Get, $"{endpoint}/{id}"))!;

    public async Task<T> CreateAsync(TDto dto) => (await api.SendJsonAsync<T>(HttpMethod.Post, endpoint, dto))!;

    public async Task<T> UpdateAsync(string id, TDto dto) => (await api.SendJsonAsync<T>(HttpMethod.Patch, $"{endpoint}/{id}", dto))!;

    public Task RemoveAsync(string id) => api.SendAsync(HttpMethod.Delete, $"{endpoint}/{id}");
}
