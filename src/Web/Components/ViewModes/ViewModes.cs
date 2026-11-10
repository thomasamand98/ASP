namespace Horizon.Web.Components.ViewModes;

// sélecteur désactivé, tout le monde en mode Onglet pour l'instant
public sealed record ViewModeDefinition(string Id, string Label, string Description, string Icon);

public static class ViewModeRegistry
{
    public const string RowExpand = "row-expand";
    public const string Tab = "tab";
    public const string Default = Tab;
    public const string StorageKey = "crud-view-mode";
    public static bool SelectionEnabled { get; } = false;

    public static readonly IReadOnlyList<ViewModeDefinition> All =
    [
        new("modal", "Modale", "Fenêtre centrée par-dessus le tableau (comportement actuel).", "viewModes.Modal"),
        new("drawer", "Panneau latéral", "Fiche qui glisse depuis le bord droit, sur toute la hauteur.", "viewModes.Drawer"),
        new("inline", "Bloc en haut", "Bloc rétractable au-dessus du tableau, qui le pousse vers le bas.", "viewModes.InlinePanel"),
        new("split", "Vue divisée", "Colonne fixe à droite du tableau (maître-détail), toujours visible.", "viewModes.SplitView"),
        new("sheet", "Panneau du bas", "Fiche qui glisse depuis le bas de l’écran, façon mobile.", "viewModes.BottomSheet"),
        new("fullscreen", "Plein écran", "La fiche recouvre toute la fenêtre, comme une page dédiée.", "viewModes.FullScreen"),
        new(RowExpand, "Ligne + bloc", "Création via le bloc du haut ; modification en dépliant la ligne dans le tableau.", "viewModes.RowExpand"),
        new(Tab, "Onglet", "Ouvre la fiche dans son propre onglet, dans la barre en haut de l’application.", "viewModes.Tab"),
    ];
}
