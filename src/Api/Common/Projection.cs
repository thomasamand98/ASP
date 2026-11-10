using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Api.Common;

// ex. /contrats?via=societes&ids=12,15 : contrats des sociétés 12 et 15
public record Projection(string Via, long[] Ids)
{
    public static Projection? Parse(string? via, string? ids)
    {
        if (string.IsNullOrEmpty(via)) return null;
        var list = (ids ?? "").Split(',').Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();
        if (!list.All(id => Regex.IsMatch(id, @"^\d+$") && long.TryParse(id, out _))) throw new BadRequestException("Paramètre ids invalide");
        return new Projection(via, list.Select(long.Parse).ToArray());
    }
}

public class ProjectionMap<T> : Dictionary<string, Func<long[], Expression<Func<T, bool>>>>;

public static class ProjectionExtensions
{
    public static IQueryable<T> WhereProjection<T>(this IQueryable<T> query, ProjectionMap<T> map, Projection? projection)
    {
        if (projection is null) return query;
        if (!map.TryGetValue(projection.Via, out var build)) throw new BadRequestException($"Projection depuis « {projection.Via} » non disponible");
        return query.Where(build(projection.Ids));
    }
}
