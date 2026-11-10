namespace Horizon.Web.Features.Planning;

// l'état vit dans PlanningPage, les vues ne font que convertir positions et horaires
public sealed record BoardContext(
    IReadOnlyList<DateTime> Days,
    IReadOnlyList<PlanningResource> Resources,
    IReadOnlyList<PlanningExecution> Executions,
    string GroupBy,
    IReadOnlyDictionary<string, double> CaByResource,
    IReadOnlyDictionary<string, int> CountByResource,
    string? SelectedId,
    DragItem? DragItem,
    DropPreview? Preview,
    Func<string?, Task> OnSelect,
    Func<DragItem, Task> OnDragStartItem,
    Func<Task> OnDragEnd,
    Func<DropPreview?, Task> OnPreview,
    Func<string, double, Task> OnDrop,
    Func<PlanningExecution, double, Task> OnResizeEnd,
    // trait "maintenant"
    double Now)
{
    // move pour une carte, copy pour une commande ou un déchargement
    public string? DropEffect => DragItem is null ? null : DragItem.Kind == "execution" ? "move" : "copy";

    public double Ca(string id) => CaByResource.GetValueOrDefault(id);

    public int Count(string id) => CountByResource.GetValueOrDefault(id);
}
