using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Marchandises;

public class MarchandiseService(AppDbContext db)
{
    private static readonly ProjectionMap<Marchandise> Projections = new()
    {
        ["commandes"] = ids => m => m.Prestations.Any(p => p.Commandes.Any(c => ids.Contains(c.IDCOMMANDES))),
    };

    public async Task<object> GetMarchandisesAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Marchandise
            .WhereSearch(search, p => m =>
                EF.Functions.Like(m.Nom_marchandise, p) || EF.Functions.Like(m.Dechet!.Description_dechet, p) || EF.Functions.Like(m.Dechet!.Code, p))
            .WhereProjection(Projections, projection);
        var marchandises = await query.OrderBy(m => m.IDMARCHANDISES).Paginate(page, pageSize)
            .Select(m => new
            {
                m.IDMARCHANDISES,
                m.Nom_marchandise,
                m.CouleurPlanning,
                m.IDDECHETS,
                Dechet = m.Dechet == null ? null : new { m.Dechet.Description_dechet, m.Dechet.Code, m.Dechet.Dangereux, m.Dechet.Autorisation, m.Dechet.Inerte, m.Dechet.Menager },
            })
            .ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { marchandises, total };
    }

    public Task<object> GetMarchandiseAsync(long id, CancellationToken ct) =>
        db.Marchandise.Where(m => m.IDMARCHANDISES == id)
            .Select(m => (object)new
            {
                m.IDMARCHANDISES,
                m.Nom_marchandise,
                m.CouleurPlanning,
                m.IDDECHETS,
                Dechet = m.Dechet == null ? null : new { m.Dechet.Description_dechet, m.Dechet.Code, m.Dechet.Dangereux, m.Dechet.Autorisation, m.Dechet.Inerte, m.Dechet.Menager },
                m.Is_dechet,
                m.Archive,
            })
            .FirstOrThrowAsync(ct);

    public async Task<object> CreateMarchandiseAsync(CreateMarchandiseDto dto, CancellationToken ct)
    {
        // défaut DB à 0, viole la FK
        var marchandise = new Marchandise { IDDECHETS = null };
        Assign(marchandise, dto);
        db.Marchandise.Add(marchandise);
        await db.SaveChangesAsync(ct);
        return await ListViewAsync(marchandise.IDMARCHANDISES, ct);
    }

    public async Task<object> UpdateMarchandiseAsync(long id, UpdateMarchandiseDto dto, CancellationToken ct)
    {
        var marchandise = await db.Marchandise.Where(m => m.IDMARCHANDISES == id).FirstOrThrowAsync(ct);
        Assign(marchandise, dto);
        await db.SaveChangesAsync(ct);
        return await ListViewAsync(id, ct);
    }

    public Task DeleteMarchandiseAsync(long id, CancellationToken ct) =>
        db.Marchandise.Where(m => m.IDMARCHANDISES == id).DeleteOrThrowAsync(ct);

    // CouleurPlanning "0" = pas de couleur (défaut WinDev)
    private static void Assign(Marchandise m, CreateMarchandiseDto dto)
    {
        dto.Nom_marchandise.Apply(v => m.Nom_marchandise = v);
        if (dto.CouleurPlanning is { IsSet: true, Value.Length: > 0 }) m.CouleurPlanning = long.Parse(dto.CouleurPlanning.Value);
        Dto.OptionalId(dto.IDDECHETS).Apply(v => m.IDDECHETS = v);
        dto.Is_dechet.ApplyInt(v => m.Is_dechet = v);
        dto.Archive.ApplyInt(v => m.Archive = v);
    }

    private Task<object> ListViewAsync(long id, CancellationToken ct) =>
        db.Marchandise.Where(m => m.IDMARCHANDISES == id)
            .Select(m => (object)new
            {
                m.IDMARCHANDISES,
                m.Nom_marchandise,
                m.CouleurPlanning,
                m.IDDECHETS,
                Dechet = m.Dechet == null ? null : new { m.Dechet.Description_dechet, m.Dechet.Code, m.Dechet.Dangereux, m.Dechet.Autorisation, m.Dechet.Inerte, m.Dechet.Menager },
            })
            .FirstOrThrowAsync(ct);
}

public class MarchandiseController(MarchandiseService service) : ApiControllerBase
{
    [HttpGet("marchandises")]
    public Task<object> GetMarchandises([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetMarchandisesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    [HttpGet("marchandises/{id}")]
    public Task<object> GetMarchandise(string id, CancellationToken ct) => service.GetMarchandiseAsync(Params.ParseId(id), ct);

    [HttpPost("marchandises")]
    public Task<IActionResult> CreateMarchandise([Body] CreateMarchandiseDto dto, CancellationToken ct) =>
        Created201(service.CreateMarchandiseAsync(dto, ct));

    [HttpPatch("marchandises/{id}")]
    public Task<object> UpdateMarchandise(string id, [Body] UpdateMarchandiseDto dto, CancellationToken ct) =>
        service.UpdateMarchandiseAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("marchandises/{id}")]
    public Task DeleteMarchandise(string id, CancellationToken ct) => service.DeleteMarchandiseAsync(Params.ParseId(id), ct);
}
