using Horizon.Web.Common;
using Horizon.Shared;
using Horizon.Shared.Models;
using Horizon.Web.Services;

namespace Horizon.Web.Features.Planning;

// ressource seulement si la carte a changé de ligne/colonne (null = Non affecté)
public sealed record MoveChanges(double Start, double End, (string? Value, bool Set) ChauffeurId = default, (string? Value, bool Set) RemorqueId = default);

// MoveExecution est appliqué tout de suite puis annulé en cas d'échec, les autres attendent la réponse
public sealed class PlanningStore(ApiClient api)
{
    private string _key = "";
    private string _loadedKey = "";
    private int _pending;

    public PlanningData? Data { get; private set; }
    public string? Error { get; private set; }
    public bool Loading => _loadedKey != _key;
    public bool Saving => _pending > 0;

    public event Action? Changed;

    private void Notify() => Changed?.Invoke();

    // réponse ignorée si la période a changé
    public async Task LoadAsync(DateTime from, DateTime to)
    {
        var key = _key = $"{Clock.ToMs(from)}|{Clock.ToMs(to)}";
        Notify();
        var query = string.Join('&',
            $"from={Uri.EscapeDataString(Clock.ToIso(Clock.ToMs(from)))}",
            $"to={Uri.EscapeDataString(Clock.ToIso(Clock.ToMs(to.AddDays(1))))}",
            $"jourDebut={DateUtils.ToInputValue(from)}",
            $"jourFin={DateUtils.ToInputValue(to)}");
        try
        {
            var data = PlanningData.From(await api.GetAsync<ApiPlanning>($"planning?{query}"));
            if (key != _key) return;
            Data = data;
            Error = null;
        }
        catch (Exception e)
        {
            if (key != _key) return;
            Error = e is ApiException ? e.Message : "Erreur de chargement du planning";
        }
        _loadedKey = key;
        Notify();
    }

    public Task ReloadAsync(DateTime from, DateTime to)
    {
        _loadedKey = "";
        Error = null;
        return LoadAsync(from, to);
    }

    private void SetData(Func<PlanningData, PlanningData> update)
    {
        if (Data is null) return;
        Data = update(Data);
        Notify();
    }

    private async Task<T> TrackAsync<T>(Func<Task<T>> action)
    {
        _pending++;
        Notify();
        try
        {
            return await action();
        }
        finally
        {
            _pending--;
            Notify();
        }
    }

    private static PlanningData Replace(PlanningData data, PlanningExecution execution) =>
        data with { Executions = [.. data.Executions.Select(e => e.Id == execution.Id ? execution : e)] };

    private static MovePlanningExecutionDto ToDto(MoveChanges changes)
    {
        var dto = new MovePlanningExecutionDto { start = Clock.ToIso(changes.Start), end = Clock.ToIso(changes.End) };
        if (changes.ChauffeurId.Set) dto.chauffeurId = changes.ChauffeurId.Value;
        if (changes.RemorqueId.Set) dto.remorqueId = changes.RemorqueId.Value;
        return dto;
    }

    public async Task MoveExecutionAsync(PlanningExecution execution, MoveChanges changes)
    {
        SetData(data => Replace(data, execution with
        {
            Start = changes.Start,
            End = changes.End,
            ChauffeurId = changes.ChauffeurId.Set ? changes.ChauffeurId.Value : execution.ChauffeurId,
            RemorqueId = changes.RemorqueId.Set ? changes.RemorqueId.Value : execution.RemorqueId,
        }));
        try
        {
            var saved = await TrackAsync(async () => (await api.SendJsonAsync<ApiExecution>(HttpMethod.Patch, $"planning/executions/{execution.Id}", ToDto(changes)))!);
            SetData(data => Replace(data, PlanningExecution.From(saved)));
        }
        catch (Exception)
        {
            SetData(data => Replace(data, execution));
            throw;
        }
    }

    public async Task<string> CreateFromCommandeAsync(string commandeId, double start, double end, string? chauffeurId, string? tracteurId, string? remorqueId)
    {
        var saved = PlanningExecution.From(await TrackAsync(async () => (await api.SendJsonAsync<ApiExecution>(HttpMethod.Post, "planning/executions",
            new CreatePlanningExecutionDto
            {
                commandeId = commandeId,
                start = Clock.ToIso(start),
                end = Clock.ToIso(end),
                chauffeurId = chauffeurId,
                tracteurId = tracteurId,
                remorqueId = remorqueId,
            }))!));
        SetData(data => data with
        {
            Executions = [.. data.Executions, saved],
            Commandes = [.. data.Commandes.Select(c => c.Id == commandeId ? c with { QtPlanifie = c.QtPlanifie + 1 } : c)],
        });
        return saved.Id;
    }

    // un déchargement en attente est déjà une exécution sans date
    public async Task PlaceDechargementAsync(string id, MoveChanges changes)
    {
        var saved = PlanningExecution.From(await TrackAsync(async () =>
            (await api.SendJsonAsync<ApiExecution>(HttpMethod.Patch, $"planning/executions/{id}", ToDto(changes)))!));
        SetData(data => data with
        {
            Executions = [.. data.Executions, saved],
            Dechargements = [.. data.Dechargements.Where(d => d.Id != id)],
        });
    }

    public async Task RemoveExecutionAsync(string id)
    {
        await TrackAsync(async () => await api.SendAsync(HttpMethod.Delete, $"planning/executions/{id}"));
        SetData(data =>
        {
            var removed = data.Executions.FirstOrDefault(e => e.Id == id);
            return data with
            {
                Executions = [.. data.Executions.Where(e => e.Id != id)],
                Dechargements = [.. data.Dechargements.Where(d => d.Id != id)],
                Commandes = [.. data.Commandes.Select(c => c.Id == removed?.CommandeId ? c with { QtPlanifie = Math.Max(0, c.QtPlanifie - 1) } : c)],
            };
        });
    }
}
