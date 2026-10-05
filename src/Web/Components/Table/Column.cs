using Horizon.Web.Common;
using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Components.Table;

// Colonne d'un DataTable (équivalent d'une colonne TanStack Table).
//   - Accessor : valeur de la cellule, utilisée pour le tri, le filtre et
//     l'export Excel (texte, nombre, booléen ou null, comme en JavaScript).
//     Sans accessor, la colonne est seulement affichée (ni tri, ni filtre).
//   - IsKey : la valeur est un champ simple de la ligne (accessorKey), et
//     non une valeur calculée — sert au libellé de l'onglet d'une fiche.
//   - Text / Template : rendu de la cellule ; par défaut, la valeur brute.
public sealed class Column<T>
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public Func<T, object?>? Accessor { get; init; }
    public bool IsKey { get; init; }
    public Func<T, string>? Text { get; init; }
    public RenderFragment<T>? Template { get; init; }

    public object? Value(T row) => Accessor?.Invoke(row);
}

// Raccourcis pour déclarer les colonnes des écrans.
public static class Col
{
    // Champ simple de la ligne (accessorKey) : l'id de la colonne est le nom
    // du champ.
    public static Column<T> Key<T>(string field, string header, Func<T, object?> accessor, Func<T, string>? text = null) =>
        new() { Id = field, Header = header, Accessor = accessor, IsKey = true, Text = text };

    // Valeur calculée (accessorFn).
    public static Column<T> Of<T>(string id, string header, Func<T, object?> accessor, Func<T, string>? text = null) =>
        new() { Id = id, Header = header, Accessor = accessor, Text = text };

    // Champ date ISO de l'API affiché en JJ/MM/AAAA (date locale) ; trié et
    // filtré sur la valeur ISO, comme dans le front d'origine.
    public static Column<T> Date<T>(string field, string header, Func<T, string?> accessor) =>
        new() { Id = field, Header = header, Accessor = accessor, IsKey = true, Text = row => Format.IsoDate(accessor(row)) };
}
