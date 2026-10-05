using Horizon.Web.Common;

namespace Horizon.Web.Components.Table;

public enum SortDirection
{
    None,
    Asc,
    Desc,
}

public sealed record TableRow<T>(int Index, T Item);

// Tri et filtres d'un DataTable, avec les règles exactes de TanStack Table
// v9 telles que configurées dans le front d'origine (tri automatique,
// filtre « includesString » sur chaque colonne) :
//   - filtre : la valeur, convertie en texte et en minuscules, contient le
//     texte saisi (en minuscules) ; un filtre vide est retiré ;
//   - tri : sur les 10 premières lignes filtrées, une valeur texte donne un
//     tri « text » (textes en minuscules comparés code par code), sinon un
//     tri « basic » (comparaison JavaScript < >) ; les égalités gardent
//     l'ordre d'origine ;
//   - premier sens : croissant si la première valeur non nulle est un texte,
//     décroissant sinon ; au clic suivant le sens s'inverse, puis le tri est
//     retiré ; Maj+clic ajoute la colonne aux tris existants.
public sealed class TableModel<T>
{
    private readonly List<(string Id, bool Desc)> _sorting = [];
    private readonly Dictionary<string, string> _filters = [];

    public IReadOnlyList<Column<T>> Columns { get; set; } = [];
    public IReadOnlyList<T> Data { get; set; } = [];

    public event Action? Changed;

    public string? FilterValue(string columnId) => _filters.GetValueOrDefault(columnId);

    public void SetFilter(string columnId, string? value)
    {
        if (string.IsNullOrEmpty(value)) _filters.Remove(columnId);
        else _filters[columnId] = value;
        Changed?.Invoke();
    }

    public SortDirection SortDirection(string columnId)
    {
        var index = _sorting.FindIndex(s => s.Id == columnId);
        return index < 0 ? Table.SortDirection.None : _sorting[index].Desc ? Table.SortDirection.Desc : Table.SortDirection.Asc;
    }

    private List<TableRow<T>> FilteredRows()
    {
        var rows = Data.Select((item, index) => new TableRow<T>(index, item)).ToList();
        foreach (var (columnId, value) in _filters)
        {
            var column = Columns.FirstOrDefault(c => c.Id == columnId);
            if (column?.Accessor is null) continue;
            var search = value.ToLowerInvariant();
            rows = rows.Where(row => column.Value(row.Item) is { } cell && JsValue.ToJsString(cell).ToLowerInvariant().Contains(search, StringComparison.Ordinal)).ToList();
        }
        return rows;
    }

    public List<TableRow<T>> Rows()
    {
        var rows = FilteredRows();
        var sorts = _sorting
            .Select(s => (Column: Columns.FirstOrDefault(c => c.Id == s.Id), s.Desc))
            .Where(s => s.Column?.Accessor is not null)
            .Select(s => (Column: s.Column!, s.Desc, Text: UsesTextSort(s.Column!, rows)))
            .ToList();
        if (sorts.Count == 0) return rows;

        int Compare(TableRow<T> a, TableRow<T> b)
        {
            foreach (var (column, desc, text) in sorts)
            {
                var x = column.Value(a.Item);
                var y = column.Value(b.Item);
                var result = text
                    ? JsValue.CompareBasic(JsValue.SortText(x).ToLowerInvariant(), JsValue.SortText(y).ToLowerInvariant())
                    : JsValue.CompareBasic(x, y);
                if (result != 0) return desc ? -result : result;
            }
            return a.Index - b.Index;
        }

        rows.Sort(Compare);
        return rows;
    }

    // Tri « text » si l'une des 10 premières lignes filtrées a une valeur texte.
    private static bool UsesTextSort(Column<T> column, List<TableRow<T>> filtered) =>
        filtered.Take(10).Any(row => column.Value(row.Item) is string);

    private bool FirstSortDescending(Column<T> column)
    {
        foreach (var row in FilteredRows().Take(10))
        {
            var value = column.Value(row.Item);
            if (value is null) continue;
            return value is not string;
        }
        return true;
    }

    public void ToggleSort(Column<T> column, bool multi)
    {
        if (column.Accessor is null) return;
        var firstDesc = FirstSortDescending(column);
        var current = SortDirection(column.Id);
        // Prochain sens : premier sens, puis l'autre, puis aucun tri.
        bool? next = current switch
        {
            Table.SortDirection.None => firstDesc,
            _ when (current == Table.SortDirection.Desc) != firstDesc => null,
            _ => current != Table.SortDirection.Desc,
        };

        var existing = _sorting.FindIndex(s => s.Id == column.Id);
        var multiMode = _sorting.Count > 0 && multi;
        if (multiMode)
        {
            if (existing < 0) _sorting.Add((column.Id, next ?? firstDesc));
            else if (next is null) _sorting.RemoveAt(existing);
            else _sorting[existing] = (column.Id, next.Value);
        }
        else
        {
            var replace = existing < 0 ? next ?? firstDesc : next;
            _sorting.Clear();
            if (replace is { } desc) _sorting.Add((column.Id, desc));
        }
        Changed?.Invoke();
    }
}
