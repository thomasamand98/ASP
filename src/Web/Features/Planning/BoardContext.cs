namespace Horizon.Web.Features.Planning;

// État et actions communs aux deux vues (ColumnView / RowView). L'état vit
// dans PlanningPage ; les vues ne font que convertir positions ↔ horaires.
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
    // Heure courante, rafraîchie chaque minute, pour le trait « maintenant ».
    double Now)
{
    // Effet de dépôt affiché pendant le glisser (« déplacer » une carte,
    // « copier » une commande ou un déchargement).
    public string? DropEffect => DragItem is null ? null : DragItem.Kind == "execution" ? "move" : "copy";

    public double Ca(string id) => CaByResource.GetValueOrDefault(id);

    public int Count(string id) => CountByResource.GetValueOrDefault(id);
}
