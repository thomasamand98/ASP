using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Layout;

public sealed record TabInfo(string Id, string Label);

// Entrée du menu latéral : soit une entrée simple directement sélectionnable
// (ex. « Tableau de bord »), soit un groupe déroulable (avec des Items).
// L'icône n'est affichée que sur les entrées principales.
public sealed record MenuNode(string Id, string Label, string? Icon = null, RenderFragment? Page = null, IReadOnlyList<MenuNode>? Items = null)
{
    public bool IsGroup => Items is not null;
}
