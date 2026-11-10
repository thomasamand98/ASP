using System.Linq.Expressions;

namespace Api.Common;

// chaque mot dans au moins une colonne, casse/accents gérés par la collation
public static class Search
{
    public static string[] Terms(string? search) =>
        (search ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // %, _ et \ échappés
    public static string Pattern(string term) =>
        $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    public static IQueryable<T> WhereSearch<T>(this IQueryable<T> query, string? search, Func<string, Expression<Func<T, bool>>> fields)
    {
        foreach (var term in Terms(search)) query = query.Where(fields(Pattern(term)));
        return query;
    }

    // seulement si page et pageSize > 0
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int? page, int? pageSize) =>
        page is { } p && pageSize is > 0 and var size ? query.Skip((p - 1) * size).Take(size) : query;
}
