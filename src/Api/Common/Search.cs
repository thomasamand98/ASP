using System.Linq.Expressions;

namespace Api.Common;

// Recherche globale des tableaux (champ « Rechercher » de CrudPage.tsx,
// envoyé en ?search=...) : chaque mot saisi doit apparaître dans au moins
// une des colonnes texte listées par le service — « dupont 06 » trouve une
// ligne dont le nom contient « dupont » ET un téléphone contenant « 06 ».
// Pas besoin de mode insensible à la casse : la collation MySQL
// utf8mb4_unicode_ci ignore déjà la casse et les accents.
public static class Search
{
    public static string[] Terms(string? search) =>
        (search ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Motif LIKE « contient le mot », caractères spéciaux échappés.
    public static string Pattern(string term) =>
        $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    // `fields` reçoit le motif LIKE d'un mot et renvoie la condition
    // « au moins une colonne le contient ».
    public static IQueryable<T> WhereSearch<T>(this IQueryable<T> query, string? search, Func<string, Expression<Func<T, bool>>> fields)
    {
        foreach (var term in Terms(search)) query = query.Where(fields(Pattern(term)));
        return query;
    }

    // Pagination des listes : active seulement si page et pageSize (> 0)
    // sont fournis.
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int? page, int? pageSize) =>
        page is { } p && pageSize is > 0 and var size ? query.Skip((p - 1) * size).Take(size) : query;
}
