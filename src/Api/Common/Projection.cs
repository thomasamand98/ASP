using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Api.Common;

// Projection (bouton « Projection » des tableaux, voir
// frontend/src/components/projection/) : liste les enregistrements d'une
// table liés aux lignes sélectionnées dans une autre, via ?via=<table
// source>&ids=1,2,3 — ex. GET /contrats?via=societes&ids=12,15 renvoie les
// contrats des sociétés 12 et 15. Chaque service déclare, par table
// source, comment filtrer ses propres lignes (ProjectionMap).
public record Projection(string Via, long[] Ids)
{
    // Lit ?via=&ids= — null sans ?via.
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
