using Horizon.Web.Common;
using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Components.Table;

// sans Accessor : ni tri ni filtre ; IsKey sert au libellé d'onglet
public sealed class Column<T>
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public Func<T, object?>? Accessor { get; init; }
    public bool IsKey { get; init; }

    // false : filtre ignoré mais entonnoir toujours affiché
    public bool Filterable { get; init; } = true;

    public Func<T, string>? Text { get; init; }
    public RenderFragment<T>? Template { get; init; }

    public object? Value(T row) => Accessor?.Invoke(row);
}

public static class Col
{
    public static Column<T> Key<T>(string field, string header, Func<T, object?> accessor, Func<T, string>? text = null) =>
        new() { Id = field, Header = header, Accessor = accessor, IsKey = true, Text = text };

    public static Column<T> Of<T>(string id, string header, Func<T, object?> accessor, Func<T, string>? text = null) =>
        new() { Id = id, Header = header, Accessor = accessor, Text = text };

    // trié et filtré sur la valeur ISO
    public static Column<T> Date<T>(string field, string header, Func<T, string?> accessor) =>
        new() { Id = field, Header = header, Accessor = accessor, IsKey = true, Text = row => Format.IsoDate(accessor(row)) };
}
