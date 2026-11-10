using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Layout;

public sealed record TabInfo(string Id, string Label);

// Items null = entrée simple, icône seulement au premier niveau
public sealed record MenuNode(string Id, string Label, string? Icon = null, RenderFragment? Page = null, IReadOnlyList<MenuNode>? Items = null)
{
    public bool IsGroup => Items is not null;
}
