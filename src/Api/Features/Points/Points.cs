using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Points;

public class PointService(AppDbContext db)
{
    private static readonly ProjectionMap<Point> Projections = new()
    {
        ["societes"] = ids => p => p.IDSOCIETES != null && ids.Contains(p.IDSOCIETES.Value),
        ["contacts"] = ids => p => p.PointContacts.Any(pc => pc.IDCONTACTS != null && ids.Contains(pc.IDCONTACTS.Value)),
    };

    public async Task<object> GetPointsAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Point
            .WhereSearch(search, p => x =>
                EF.Functions.Like(x.Libelle, p) || EF.Functions.Like(x.Nom_societe, p) || EF.Functions.Like(x.Telephone, p)
                || EF.Functions.Like(x.Adresse!.Adresse1, p) || EF.Functions.Like(x.Adresse!.CP, p) || EF.Functions.Like(x.Adresse!.Localite, p))
            .WhereProjection(Projections, projection);
        var points = await ListSelect(query.OrderBy(p => p.IDPOINTS).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { points, total };
    }

    // plages horaires sans FK (IDLIENS générique), lues à part, en HH:MM
    public async Task<object> GetPointAsync(long id, CancellationToken ct)
    {
        var point = await db.Point.AsNoTracking().Include(p => p.Adresse).Include(p => p.ContactDefauts).Where(p => p.IDPOINTS == id).FirstOrThrowAsync(ct);
        var pointContacts = await db.PointContact.Where(pc => pc.IDPOINTS == id).OrderBy(pc => pc.IDPOINT_CONTACTS)
            .Select(pc => new
            {
                pc.IDCONTACTS,
                pc.Lien,
                pc.Recevoir_Mail_Planning,
                Contact = pc.Contact == null ? null : new
                {
                    pc.Contact.Civilite,
                    pc.Contact.Nom_contact,
                    pc.Contact.Prenom_contact,
                    pc.Contact.Telephone_portable,
                    pc.Contact.Telephone_fixe,
                    pc.Contact.E_mail,
                },
            })
            .ToListAsync(ct);
        var horaires = await db.GrilleHoraire.Where(h => h.IDLIENS == id).OrderBy(h => h.Jour_semaine).ThenBy(h => h.Heure_debut)
            .Select(h => new { h.Jour_semaine, h.Heure_debut, h.Heure_fin })
            .ToListAsync(ct);
        return new
        {
            point.IDPOINTS,
            point.Libelle,
            point.Nom_societe,
            point.Telephone,
            point.IDADRESSES,
            point.IDSOCIETES,
            Adresse = AdresseView.From(point.Adresse),
            point.Archive,
            point.Lien_googleMap,
            point.Instruction,
            point.IDCONTACTS_DEFAUTS,
            ContactDefauts = point.ContactDefauts is null ? null : new { point.ContactDefauts.Nom_contact, point.ContactDefauts.Prenom_contact },
            PointContacts = pointContacts,
            Horaires = horaires.Select(h => new { h.Jour_semaine, Heure_debut = ToHhmm(h.Heure_debut), Heure_fin = ToHhmm(h.Heure_fin) }),
        };
    }

    public async Task<object> CreatePointAsync(CreatePointDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, null, Adresses.Pick(dto), ct);
        // NULL explicite, défaut DB à 0 (FK)
        var point = new Point { IDADRESSES = idAdresses, IDSOCIETES = null, IDCONTACTS_DEFAUTS = null, Date_heure_creation = DateTime.UtcNow };
        Assign(point, dto);
        db.Point.Add(point);
        await db.SaveChangesAsync(ct);
        if (dto.Horaires.IsSet) await ReplaceHorairesAsync(point.IDPOINTS, dto.Horaires.Value, ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Point.Where(p => p.IDPOINTS == point.IDPOINTS)).FirstOrThrowAsync(ct);
    }

    public async Task<object> UpdatePointAsync(long id, UpdatePointDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var point = await db.Point.Where(p => p.IDPOINTS == id).FirstOrThrowAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, point.IDADRESSES, Adresses.Pick(dto), ct);
        if (dto.Horaires.IsSet) await ReplaceHorairesAsync(id, dto.Horaires.Value, ct);
        Assign(point, dto);
        point.IDADRESSES = idAdresses;
        point.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Point.Where(p => p.IDPOINTS == id)).FirstOrThrowAsync(ct);
    }

    public Task DeletePointAsync(long id, CancellationToken ct) => db.Point.Where(p => p.IDPOINTS == id).DeleteOrThrowAsync(ct);

    // pas de contrainte d'unicité, donc tous les liens du contact
    public async Task UpdatePointContactAsync(long pointId, long contactId, UpdatePointContactDto dto, CancellationToken ct)
    {
        var links = await db.PointContact.Where(pc => pc.IDPOINTS == pointId && pc.IDCONTACTS == contactId).ToListAsync(ct);
        if (links.Count == 0) throw new NotFoundException("Ce contact n’est pas lié à ce point.");
        foreach (var link in links) dto.Recevoir_Mail_Planning.ApplyInt(v => link.Recevoir_Mail_Planning = v);
        await db.SaveChangesAsync(ct);
    }

    // supprime juste le lien, pas le contact
    public Task RemovePointContactAsync(long pointId, long contactId, CancellationToken ct) =>
        db.PointContact.Where(pc => pc.IDPOINTS == pointId && pc.IDCONTACTS == contactId).ExecuteDeleteAsync(ct);

    private static void Assign(Point p, CreatePointDto dto)
    {
        dto.Libelle.Apply(v => p.Libelle = v);
        dto.Nom_societe.Apply(v => p.Nom_societe = v);
        dto.Telephone.Apply(v => p.Telephone = v);
        if (dto.IDSOCIETES is { IsSet: true, Value.Length: > 0 }) p.IDSOCIETES = Dto.Id(dto.IDSOCIETES.Value);
        dto.Archive.ApplyInt(v => p.Archive = v);
        dto.Lien_googleMap.Apply(v => p.Lien_googleMap = v);
        dto.Instruction.Apply(v => p.Instruction = v);
        if (dto.IDCONTACTS_DEFAUTS is { IsSet: true, Value.Length: > 0 }) p.IDCONTACTS_DEFAUTS = Dto.Id(dto.IDCONTACTS_DEFAUTS.Value);
    }

    private async Task ReplaceHorairesAsync(long pointId, List<HorairePointDto> horaires, CancellationToken ct)
    {
        await db.GrilleHoraire.Where(h => h.IDLIENS == pointId).ExecuteDeleteAsync(ct);
        if (horaires.Count == 0) return;
        var now = DateTime.UtcNow;
        db.GrilleHoraire.AddRange(horaires.Select(h => new GrilleHoraire
        {
            IDLIENS = pointId,
            Jour_semaine = h.Jour_semaine.Value,
            Heure_debut = TimeSpan.Parse(h.Heure_debut.Value),
            Heure_fin = TimeSpan.Parse(h.Heure_fin.Value),
            Horaire = $"{h.Heure_debut.Value} - {h.Heure_fin.Value}",
            Date_heure_creation = now,
        }));
        await db.SaveChangesAsync(ct);
    }

    private static string ToHhmm(TimeSpan? value) => value is { } time ? JsonConfig.FormatTime(time).Substring(11, 5) : "";

    private static IQueryable<object> ListSelect(IQueryable<Point> query) => query.Select(p => new
    {
        p.IDPOINTS,
        p.Libelle,
        p.Nom_societe,
        p.Telephone,
        p.IDADRESSES,
        p.IDSOCIETES,
        Adresse = p.Adresse == null ? null : new { p.Adresse.Adresse1, p.Adresse.CP, p.Adresse.Localite },
    });
}

public class PointController(PointService service) : ApiControllerBase
{
    [HttpGet("points")]
    public Task<object> GetPoints([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetPointsAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    [HttpGet("points/{id}")]
    public Task<object> GetPoint(string id, CancellationToken ct) => service.GetPointAsync(Params.ParseId(id), ct);

    [HttpPost("points")]
    public Task<IActionResult> CreatePoint([Body] CreatePointDto dto, CancellationToken ct) => Created201(service.CreatePointAsync(dto, ct));

    [HttpPatch("points/{id}")]
    public Task<object> UpdatePoint(string id, [Body] UpdatePointDto dto, CancellationToken ct) => service.UpdatePointAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("points/{id}")]
    public Task DeletePoint(string id, CancellationToken ct) => service.DeletePointAsync(Params.ParseId(id), ct);

    [HttpPatch("points/{id}/contacts/{contactId}")]
    public Task UpdatePointContact(string id, string contactId, [Body] UpdatePointContactDto dto, CancellationToken ct) =>
        service.UpdatePointContactAsync(Params.ParseId(id), Params.ParseId(contactId), dto, ct);

    [HttpDelete("points/{id}/contacts/{contactId}")]
    public Task RemovePointContact(string id, string contactId, CancellationToken ct) =>
        service.RemovePointContactAsync(Params.ParseId(id), Params.ParseId(contactId), ct);
}
