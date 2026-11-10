using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Enumerations;

public class EnumerationService(AppDbContext db)
{
    // tri par Ordre puis libellé, comme les listes déroulantes
    public async Task<object> GetEnumerationsAsync(long? categorieId, string? categorieNom, CancellationToken ct)
    {
        IQueryable<Enumeration> query = db.Enumeration;
        if (categorieId is not null) query = query.Where(e => e.IDCATEGORIES_ENUMERATION == categorieId);
        if (!string.IsNullOrEmpty(categorieNom)) query = query.Where(e => e.CategorieEnumeration!.Nom == categorieNom);
        var enumerations = await Select(query.OrderBy(e => e.Ordre).ThenBy(e => e.Valeur_affiche).ThenBy(e => e.IDENUMERATIONS)).ToListAsync(ct);
        return new { enumerations };
    }

    public async Task<object> CreateEnumerationAsync(CreateEnumerationDto dto, CancellationToken ct)
    {
        // défaut DB à 0 (FK), pas encore d'auth pour les remplir
        var enumeration = new Enumeration
        {
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = DateTime.UtcNow,
            Date_heure_modification = DateTime.UtcNow,
        };
        Assign(enumeration, dto);
        db.Enumeration.Add(enumeration);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(enumeration.IDENUMERATIONS, ct);
    }

    public async Task<object> UpdateEnumerationAsync(long id, UpdateEnumerationDto dto, CancellationToken ct)
    {
        var enumeration = await db.Enumeration.Where(e => e.IDENUMERATIONS == id).FirstOrThrowAsync(ct);
        Assign(enumeration, dto);
        enumeration.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ViewAsync(id, ct);
    }

    public async Task DeleteEnumerationAsync(long id, CancellationToken ct)
    {
        var valeurSystem = await db.Enumeration.Where(e => e.IDENUMERATIONS == id).Select(e => new { e.Valeur_system }).FirstOrThrowAsync(ct);
        if (valeurSystem.Valeur_system is not (null or 0)) throw new ConflictException("Une valeur système ne peut pas être supprimée");
        await db.Enumeration.Where(e => e.IDENUMERATIONS == id).DeleteOrThrowAsync(ct);
    }

    private static void Assign(Enumeration e, CreateEnumerationDto dto)
    {
        if (dto.IDCATEGORIES_ENUMERATION is { IsSet: true, Value.Length: > 0 }) e.IDCATEGORIES_ENUMERATION = Dto.Id(dto.IDCATEGORIES_ENUMERATION.Value);
        dto.Valeur_affiche.Apply(v => e.Valeur_affiche = v);
        dto.Valeur.Apply(v => e.Valeur = v);
        dto.Ordre.ApplyInt(v => e.Ordre = v);
        dto.Valeur_associee.Apply(v => e.Valeur_associee = v);
        dto.Valeur_system.ApplyInt(v => e.Valeur_system = v);
    }

    private static IQueryable<object> Select(IQueryable<Enumeration> query) =>
        query.Select(e => new { e.IDENUMERATIONS, e.IDCATEGORIES_ENUMERATION, e.Valeur_affiche, e.Valeur, e.Ordre, e.Valeur_associee, e.Valeur_system });

    private Task<object> ViewAsync(long id, CancellationToken ct) => Select(db.Enumeration.Where(e => e.IDENUMERATIONS == id)).FirstOrThrowAsync(ct);
}

public class EnumerationController(EnumerationService service) : ApiControllerBase
{
    [HttpGet("enumerations")]
    public Task<object> GetEnumerations([FromQuery] string? categorieId, [FromQuery] string? categorie, CancellationToken ct) =>
        service.GetEnumerationsAsync(Params.ParseOptionalId(categorieId), categorie, ct);

    [HttpPost("enumerations")]
    public Task<IActionResult> CreateEnumeration([Body] CreateEnumerationDto dto, CancellationToken ct) =>
        Created201(service.CreateEnumerationAsync(dto, ct));

    [HttpPatch("enumerations/{id}")]
    public Task<object> UpdateEnumeration(string id, [Body] UpdateEnumerationDto dto, CancellationToken ct) =>
        service.UpdateEnumerationAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("enumerations/{id}")]
    public Task DeleteEnumeration(string id, CancellationToken ct) => service.DeleteEnumerationAsync(Params.ParseId(id), ct);
}
