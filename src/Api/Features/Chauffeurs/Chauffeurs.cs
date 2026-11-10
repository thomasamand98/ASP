using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Chauffeurs;

public class ChauffeurService(AppDbContext db)
{
    private static readonly ProjectionMap<Chauffeur> Projections = new()
    {
        ["societes"] = ids => c => c.IDSOCIETES != null && ids.Contains(c.IDSOCIETES.Value),
        ["personnel"] = ids => c => c.IDPERSONNELS != null && ids.Contains(c.IDPERSONNELS.Value),
        ["attelages"] = ids => c => c.AttelageReferences.Any(a => ids.Contains(a.IDATTELAGE_REFERENCE)),
    };

    // archive null = tous, false inclut les NULL
    public async Task<object> GetChauffeursAsync(int? page, int? pageSize, string? search, Projection? projection, bool? archive, CancellationToken ct)
    {
        var query = db.Chauffeur
            .WhereSearch(search, p => c =>
                EF.Functions.Like(c.Nom_chauffeur, p) || EF.Functions.Like(c.Categorie, p) || EF.Functions.Like(c.Telephone, p)
                || EF.Functions.Like(c.Societe!.Nom_societe, p) || EF.Functions.Like(c.Personnel!.Nom_Personnel, p) || EF.Functions.Like(c.Personnel!.Prenom_Personnel, p))
            .WhereProjection(Projections, projection);
        if (archive == true) query = query.Where(c => c.Archive == 1);
        if (archive == false) query = query.Where(c => c.Archive == 0 || c.Archive == null);

        var chauffeurs = await Select(query.OrderBy(c => c.IDCHAUFFEURS).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { chauffeurs, total };
    }

    public Task<object> GetChauffeurAsync(long id, CancellationToken ct) =>
        Select(db.Chauffeur.Where(c => c.IDCHAUFFEURS == id)).FirstOrThrowAsync(ct);

    public async Task<object> CreateChauffeurAsync(CreateChauffeurDto dto, CancellationToken ct)
    {
        // défaut DB à 0, viole la FK
        var chauffeur = new Chauffeur { IDPERSONNELS = null, IDSOCIETES = null };
        Assign(chauffeur, dto);
        db.Chauffeur.Add(chauffeur);
        await db.SaveChangesAsync(ct);
        return await GetChauffeurAsync(chauffeur.IDCHAUFFEURS, ct);
    }

    public async Task<object> UpdateChauffeurAsync(long id, UpdateChauffeurDto dto, CancellationToken ct)
    {
        var chauffeur = await db.Chauffeur.Where(c => c.IDCHAUFFEURS == id).FirstOrThrowAsync(ct);
        Assign(chauffeur, dto);
        await db.SaveChangesAsync(ct);
        return await GetChauffeurAsync(id, ct);
    }

    public Task DeleteChauffeurAsync(long id, CancellationToken ct) => db.Chauffeur.Where(c => c.IDCHAUFFEURS == id).DeleteOrThrowAsync(ct);

    // "" = NULL (FK)
    private static void Assign(Chauffeur c, CreateChauffeurDto dto)
    {
        dto.Nom_chauffeur.Apply(v => c.Nom_chauffeur = v);
        dto.Telephone.Apply(v => c.Telephone = v);
        dto.Categorie.Apply(v => c.Categorie = v);
        dto.Archive.ApplyInt(v => c.Archive = v);
        Dto.OptionalId(dto.IDPERSONNELS).Apply(v => c.IDPERSONNELS = v);
        Dto.OptionalId(dto.IDSOCIETES).Apply(v => c.IDSOCIETES = v);
    }

    private static IQueryable<object> Select(IQueryable<Chauffeur> query) => query.Select(c => new
    {
        c.IDCHAUFFEURS,
        c.Nom_chauffeur,
        c.Categorie,
        c.Telephone,
        c.Archive,
        c.IDSOCIETES,
        Societe = c.Societe == null ? null : new { c.Societe.Nom_societe },
        c.IDPERSONNELS,
        Personnel = c.Personnel == null ? null : new { c.Personnel.Nom_Personnel, c.Personnel.Prenom_Personnel, c.Personnel.E_mail_professionnel },
    });
}

public class ChauffeurController(ChauffeurService service) : ApiControllerBase
{
    [HttpGet("chauffeurs")]
    public Task<object> GetChauffeurs([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, [FromQuery] string? archive, CancellationToken ct) =>
        service.GetChauffeursAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), Params.ParseArchive(archive), ct);

    [HttpPost("chauffeurs")]
    public Task<IActionResult> CreateChauffeur([Body] CreateChauffeurDto dto, CancellationToken ct) => Created201(service.CreateChauffeurAsync(dto, ct));

    [HttpPatch("chauffeurs/{id}")]
    public Task<object> UpdateChauffeur(string id, [Body] UpdateChauffeurDto dto, CancellationToken ct) =>
        service.UpdateChauffeurAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("chauffeurs/{id}")]
    public Task DeleteChauffeur(string id, CancellationToken ct) => service.DeleteChauffeurAsync(Params.ParseId(id), ct);
}
