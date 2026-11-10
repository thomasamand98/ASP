using System.Text.Json;
using Horizon.Shared;

namespace Horizon.Web.Services;

// endpoint = chemin du GET et clé du tableau dans la réponse, sauf si key est fourni
public sealed class ApiList<T>(ApiClient api, string endpoint, string? key = null)
{
    // pour ignorer les réponses arrivées dans le désordre
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

    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    public Task SetPageAsync(int page)
    {
        Page = page;
        return RefetchAsync();
    }

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

    // filtre identique : pas de rechargement
    public Task SetFiltersAsync(IReadOnlyDictionary<string, string> filters, bool resetPage = false)
    {
        var pageChanged = resetPage && Page != 1;
        if (resetPage) Page = 1;
        if (filters.Count == Filters.Count && filters.All(f => Filters.TryGetValue(f.Key, out var v) && v == f.Value)) return pageChanged ? RefetchAsync() : Task.CompletedTask;
        Filters = filters;
        return RefetchAsync();
    }

    // avant le premier chargement, sans requête
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

// T = ligne de liste, TDetail = fiche complète
public sealed class CrudApi<TDto, T, TDetail>(ApiClient api, string endpoint)
{
    public async Task<TDetail> GetAsync(string id) => (await api.SendJsonAsync<TDetail>(HttpMethod.Get, $"{endpoint}/{id}"))!;

    public async Task<T> CreateAsync(TDto dto) => (await api.SendJsonAsync<T>(HttpMethod.Post, endpoint, dto))!;

    public async Task<T> UpdateAsync(string id, TDto dto) => (await api.SendJsonAsync<T>(HttpMethod.Patch, $"{endpoint}/{id}", dto))!;

    public Task RemoveAsync(string id) => api.SendAsync(HttpMethod.Delete, $"{endpoint}/{id}");
}
