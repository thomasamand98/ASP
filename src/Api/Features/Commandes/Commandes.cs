using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Commandes;

public class CommandeService(AppDbContext db)
{
    private sealed class Row
    {
        public long IDCOMMANDES { get; init; }
        public DateTime? Date_commande { get; init; }
        public long? IDCONTRATS { get; init; }
        public bool HasContrat { get; init; }
        public string? Num_contrat { get; init; }
        public string? Version_contrat { get; init; }
        public DateTime? Date_debut { get; init; }
        public DateTime? Date_fin { get; init; }
        public bool HasSociete { get; init; }
        public string? Nom_societe { get; init; }
        public long? IDPRESTATIONS { get; init; }
        public bool HasPrestation { get; init; }
        public string? Description_prestation { get; init; }
        public int? QT { get; init; }
        public int? QT_planifie { get; init; }
        public string? Statut { get; init; }
        public string? NumRef { get; init; }
        public string? Instruction { get; init; }
    }

    private static readonly ProjectionMap<Commande> Projections = new()
    {
        ["contrats"] = ids => c => c.IDCONTRATS != null && ids.Contains(c.IDCONTRATS.Value),
        ["marchandises"] = ids => c => c.Contrat!.Prestations.Any(p => ids.Contains(p.Marchandise!.IDMARCHANDISES)),
    };

    public async Task<object> GetCommandesAsync(int? page, int? pageSize, string? search, Projection? projection, Period? period, CancellationToken ct)
    {
        var query = db.Commande
            .WhereSearch(search, p => c =>
                EF.Functions.Like(c.NumRef, p) || EF.Functions.Like(c.Instruction, p) || EF.Functions.Like(c.Contrat!.Num_contrat, p)
                || EF.Functions.Like(c.Contrat!.Societe!.Nom_societe, p)
                || c.Contrat!.Prestations.Any(prestation => EF.Functions.Like(prestation.Marchandise!.Nom_marchandise, p)))
            .WhereProjection(Projections, projection);
        if (period?.Gte is { } from) query = query.Where(c => c.Date_commande >= from);
        if (period?.Lte is { } to) query = query.Where(c => c.Date_commande <= to);

        var rows = await Select(query.OrderBy(c => c.IDCOMMANDES).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { commandes = await WithMarchandiseAsync(rows, ct), total };
    }

    public async Task<object> GetCommandeAsync(long id, CancellationToken ct)
    {
        var row = await Select(db.Commande.Where(c => c.IDCOMMANDES == id)).FirstOrThrowAsync(ct);
        return (await WithMarchandiseAsync([row], ct))[0];
    }

    public async Task<object> CreateCommandeAsync(CreateCommandeDto dto, CancellationToken ct)
    {
        // 0 par défaut en base, viole les FK
        var commande = new Commande { IDCONTRATS = null, IDPRESTATIONS = null, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        Assign(commande, dto);
        db.Commande.Add(commande);
        await db.SaveChangesAsync(ct);
        return await GetCommandeAsync(commande.IDCOMMANDES, ct);
    }

    public async Task<object> UpdateCommandeAsync(long id, UpdateCommandeDto dto, CancellationToken ct)
    {
        var commande = await db.Commande.Where(c => c.IDCOMMANDES == id).FirstOrThrowAsync(ct);
        Assign(commande, dto);
        await db.SaveChangesAsync(ct);
        return await GetCommandeAsync(id, ct);
    }

    public Task DeleteCommandeAsync(long id, CancellationToken ct) => db.Commande.Where(c => c.IDCOMMANDES == id).DeleteOrThrowAsync(ct);

    private static void Assign(Commande c, CreateCommandeDto dto)
    {
        dto.Date_commande.ApplyDate(v => c.Date_commande = v, dateOnly: true);
        Dto.OptionalId(dto.IDCONTRATS).Apply(v => c.IDCONTRATS = v);
        Dto.OptionalId(dto.IDPRESTATIONS).Apply(v => c.IDPRESTATIONS = v);
        dto.QT.ApplyInt(v => c.QT = v);
        dto.QT_planifie.ApplyInt(v => c.QT_planifie = v);
        dto.Statut.Apply(v => c.Statut = v);
        dto.NumRef.Apply(v => c.NumRef = v);
        dto.Instruction.Apply(v => c.Instruction = v);
    }

    private static IQueryable<Row> Select(IQueryable<Commande> query) => query.Select(c => new Row
    {
        IDCOMMANDES = c.IDCOMMANDES,
        Date_commande = c.Date_commande,
        IDCONTRATS = c.IDCONTRATS,
        HasContrat = c.Contrat != null,
        Num_contrat = c.Contrat!.Num_contrat,
        Version_contrat = c.Contrat!.Version_contrat,
        Date_debut = c.Contrat!.Date_debut,
        Date_fin = c.Contrat!.Date_fin,
        HasSociete = c.Contrat!.Societe != null,
        Nom_societe = c.Contrat!.Societe!.Nom_societe,
        IDPRESTATIONS = c.IDPRESTATIONS,
        HasPrestation = c.Prestation != null,
        Description_prestation = c.Prestation!.Description_prestation,
        QT = c.QT,
        QT_planifie = c.QT_planifie,
        Statut = c.Statut,
        NumRef = c.NumRef,
        Instruction = c.Instruction,
    });

    // marchandise/unité = celles de la 1re prestation du contrat, comme WinDev
    private async Task<List<object>> WithMarchandiseAsync(List<Row> rows, CancellationToken ct)
    {
        var contratIds = rows.Where(r => r.HasContrat).Select(r => r.IDCONTRATS).Distinct().ToList();
        var prestations = contratIds.Count == 0
            ? []
            : await db.Prestation.Where(p => contratIds.Contains(p.IDCONTRATS))
                .Select(p => new { p.IDCONTRATS, p.Ordre, p.IDPRESTATIONS, p.Unite, HasMarchandise = p.Marchandise != null, p.Marchandise!.Nom_marchandise })
                .ToListAsync(ct);
        // NULL en premier comme MySQL, puis id
        var first = prestations.GroupBy(p => p.IDCONTRATS!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Ordre.HasValue).ThenBy(p => p.Ordre).ThenBy(p => p.IDPRESTATIONS).First());

        return rows.Select(r =>
        {
            var prestation = r.HasContrat ? first.GetValueOrDefault(r.IDCONTRATS!.Value) : null;
            return (object)new
            {
                r.IDCOMMANDES,
                r.Date_commande,
                r.IDCONTRATS,
                Contrat = r.HasContrat
                    ? new { r.Num_contrat, r.Version_contrat, r.Date_debut, r.Date_fin, Societe = r.HasSociete ? new { r.Nom_societe } : null }
                    : null,
                r.IDPRESTATIONS,
                Prestation = r.HasPrestation ? new { r.Description_prestation } : null,
                r.QT,
                r.QT_planifie,
                r.Statut,
                r.NumRef,
                r.Instruction,
                Marchandise = prestation is { HasMarchandise: true } ? new { prestation.Nom_marchandise } : null,
                Unite = prestation?.Unite is null or 0 ? null : prestation.Unite,
            };
        }).ToList();
    }
}

public class CommandeController(CommandeService service) : ApiControllerBase
{
    // from/to inclus, sur Date_commande
    [HttpGet("commandes")]
    public Task<object> GetCommandes([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        service.GetCommandesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), Params.ParsePeriod(from, to), ct);

    [HttpPost("commandes")]
    public Task<IActionResult> CreateCommande([Body] CreateCommandeDto dto, CancellationToken ct) => Created201(service.CreateCommandeAsync(dto, ct));

    [HttpPatch("commandes/{id}")]
    public Task<object> UpdateCommande(string id, [Body] UpdateCommandeDto dto, CancellationToken ct) =>
        service.UpdateCommandeAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("commandes/{id}")]
    public Task DeleteCommande(string id, CancellationToken ct) => service.DeleteCommandeAsync(Params.ParseId(id), ct);
}
