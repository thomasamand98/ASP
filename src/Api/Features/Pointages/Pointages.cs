using System.Text.RegularExpressions;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Pointages;

public partial class PointageService(AppDbContext db)
{
    // au-delà la feuille est illisible
    private const int MaxFeuilleDays = 93;

    private static readonly ProjectionMap<Pointage> Projections = new()
    {
        ["personnel"] = ids => p => p.IDPERSONNELS != null && ids.Contains(p.IDPERSONNELS.Value),
    };

    public async Task<object> GetPointagesAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Pointage
            .WhereSearch(search, p => x =>
                EF.Functions.Like(x.Personnel!.Nom_Personnel, p) || EF.Functions.Like(x.Personnel!.Prenom_Personnel, p)
                || EF.Functions.Like(x.TypeStatut!.Libelle_generique, p) || EF.Functions.Like(x.Remarque, p))
            .WhereProjection(Projections, projection);
        var pointage = await ListSelect(query.OrderBy(p => p.IDPOINTAGES).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { pointage, total };
    }

    // archivés compris, le formulaire peut avoir besoin de celui du pointage en cours
    public async Task<object> GetStatutsAsync(CancellationToken ct)
    {
        var statuts = await db.TypeStatut.OrderBy(s => s.Ordre).ThenBy(s => s.Libelle_generique)
            .Select(s => new { s.IDType_Statut, s.Libelle_generique, s.CouleurStatut, s.Statut_de_travail, s.Archive })
            .ToListAsync(ct);
        return new { statuts };
    }

    // taux = celui en vigueur à la fin de la période
    public async Task<object> GetFeuilleAsync(string? personnelIdRaw, string? fromRaw, string? toRaw, CancellationToken ct)
    {
        if (personnelIdRaw is null || !Digits().IsMatch(personnelIdRaw)) throw new BadRequestException("Salarié invalide.");
        var from = Params.ParseDay(fromRaw, "from");
        var to = Params.ParseDay(toRaw, "to");
        if (to < from) throw new BadRequestException("La fin de la période doit être après son début.");
        if ((to - from).TotalDays >= MaxFeuilleDays) throw new BadRequestException($"La période ne peut pas dépasser {MaxFeuilleDays} jours.");
        var personnelId = Dto.Id(personnelIdRaw);

        // + couleur et "travaillé" du statut
        var pointages = await db.Pointage
            .Where(p => p.IDPERSONNELS == personnelId && p.Date_application >= from && p.Date_application <= to)
            .OrderBy(p => p.Date_application).ThenBy(p => p.Date_heure_debut).ThenBy(p => p.IDPOINTAGES)
            .Select(p => new
            {
                p.IDPOINTAGES,
                p.Date_application,
                p.IDPERSONNELS,
                Personnel = p.Personnel == null ? null : new { p.Personnel.Nom_Personnel, p.Personnel.Prenom_Personnel },
                p.Date_heure_debut,
                p.Date_heure_fin,
                p.Heure_coupure,
                p.Heure_nuit,
                p.Heure_liaison,
                p.Heure_Stanby,
                p.IDType_Statut,
                TypeStatut = p.TypeStatut == null ? null : new { p.TypeStatut.Libelle_generique, p.TypeStatut.CouleurStatut, p.TypeStatut.Statut_de_travail },
                p.Nuitee,
                p.Remarque,
                p.Debut_pause,
                p.Fin_Pause,
                p.Heure_jour,
            })
            .ToListAsync(ct);
        var taux = await db.TauxHoraireContratTravail
            .Where(t => t.ContratTravail!.IDPERSONNELS == personnelId && (t.Date_application == null || t.Date_application <= to))
            .OrderByDescending(t => t.Date_application).ThenByDescending(t => t.IDTAUX_HORAIRE)
            .Select(t => new
            {
                t.Date_application,
                t.Taux_horaire,
                t.Taux_horaire_supplementaire,
                t.Taux_horaire_nuitee,
                t.Taux_nuitee,
                t.Taux_samedi,
                t.Taux_cheque_repas,
                t.Taux_stanby,
            })
            .FirstOrDefaultAsync(ct);
        return new { pointages, taux };
    }

    public Task<object> GetPointageAsync(long id, CancellationToken ct) =>
        db.Pointage.Where(p => p.IDPOINTAGES == id)
            .Select(p => (object)new
            {
                p.IDPOINTAGES,
                p.Date_application,
                p.IDPERSONNELS,
                Personnel = p.Personnel == null ? null : new { p.Personnel.Nom_Personnel, p.Personnel.Prenom_Personnel },
                p.Date_heure_debut,
                p.Date_heure_fin,
                p.Heure_coupure,
                p.Heure_nuit,
                p.Heure_liaison,
                p.Heure_Stanby,
                p.IDType_Statut,
                TypeStatut = p.TypeStatut == null ? null : new { p.TypeStatut.Libelle_generique },
                p.Nuitee,
                p.Remarque,
                p.Debut_pause,
                p.Fin_Pause,
                p.Heure_jour,
            })
            .FirstOrThrowAsync(ct);

    public async Task<object> CreatePointageAsync(CreatePointageDto dto, CancellationToken ct)
    {
        // défaut DB à 0 sur les FK, d'où les NULL
        var pointage = new Pointage
        {
            IDPERSONNELS = null,
            IDType_Statut = null,
            Heure_Stanby = TimeSpan.Zero,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
        };
        Assign(pointage, dto);
        db.Pointage.Add(pointage);
        await db.SaveChangesAsync(ct);
        return await ListSelect(db.Pointage.Where(p => p.IDPOINTAGES == pointage.IDPOINTAGES)).FirstOrThrowAsync(ct);
    }

    public async Task<object> UpdatePointageAsync(long id, UpdatePointageDto dto, CancellationToken ct)
    {
        // validation avant lecture : 400 avant 404
        Assign(new Pointage(), dto);
        var pointage = await db.Pointage.Where(p => p.IDPOINTAGES == id).FirstOrThrowAsync(ct);
        Assign(pointage, dto);
        await db.SaveChangesAsync(ct);
        return await ListSelect(db.Pointage.Where(p => p.IDPOINTAGES == id)).FirstOrThrowAsync(ct);
    }

    public Task DeletePointageAsync(long id, CancellationToken ct) => db.Pointage.Where(p => p.IDPOINTAGES == id).DeleteOrThrowAsync(ct);

    // Heure_* et pauses : "HH:MM" ou datetime du 01/01/1970, "" = NULL
    private static void Assign(Pointage p, CreatePointageDto dto)
    {
        var standby = ToTime(dto.Heure_Stanby, "Heure_Stanby");
        if (dto.IDPERSONNELS is { IsSet: true, Value.Length: > 0 }) p.IDPERSONNELS = Dto.Id(dto.IDPERSONNELS.Value);
        if (dto.IDType_Statut is { IsSet: true, Value.Length: > 0 }) p.IDType_Statut = Dto.Id(dto.IDType_Statut.Value);
        if (dto.Date_application.IsSet)
        {
            var raw = dto.Date_application.Value;
            p.Date_application = raw.Length == 0 ? null : Params.ParseDay(raw.Length > 10 ? raw[..10] : raw, "Date_application");
        }
        ToInstant(dto.Date_heure_debut, "Date_heure_debut").Apply(v => p.Date_heure_debut = v);
        ToInstant(dto.Date_heure_fin, "Date_heure_fin").Apply(v => p.Date_heure_fin = v);
        ToTime(dto.Heure_coupure, "Heure_coupure").Apply(v => p.Heure_coupure = v);
        ToTime(dto.Heure_liaison, "Heure_liaison").Apply(v => p.Heure_liaison = v);
        ToTime(dto.Heure_nuit, "Heure_nuit").Apply(v => p.Heure_nuit = v);
        ToTime(dto.Heure_jour, "Heure_jour").Apply(v => p.Heure_jour = v);
        ToTime(dto.Debut_pause, "Debut_pause").Apply(v => p.Debut_pause = v);
        ToTime(dto.Fin_Pause, "Fin_Pause").Apply(v => p.Fin_Pause = v);
        // NOT NULL, vide = 00:00
        standby.Apply(v => p.Heure_Stanby = v ?? TimeSpan.Zero);
        dto.Nuitee.ApplyInt(v => p.Nuitee = v);
        dto.Remarque.Apply(v => p.Remarque = v);
    }

    private static Opt<DateTime?> ToInstant(Opt<string> raw, string field)
    {
        if (!raw.IsSet) return default;
        if (raw.Value.Length == 0) return new Opt<DateTime?>(null);
        if (!JsDate.TryParse(raw.Value, out var date)) throw new BadRequestException($"Date invalide pour « {field} ».");
        return new Opt<DateTime?>(date);
    }

    private static Opt<TimeSpan?> ToTime(Opt<string> raw, string field)
    {
        if (!raw.IsSet) return default;
        if (raw.Value.Length == 0) return new Opt<TimeSpan?>(null);
        var value = raw.Value;
        var instant = HhMm().IsMatch(value) ? $"1970-01-01T{(value.Length == 5 ? $"{value}:00" : value)}Z" : value;
        if (!JsDate.TryParse(instant, out var date)) throw new BadRequestException($"Date invalide pour « {field} ».");
        return new Opt<TimeSpan?>(date.TimeOfDay);
    }

    private static IQueryable<object> ListSelect(IQueryable<Pointage> query) => query.Select(p => new
    {
        p.IDPOINTAGES,
        p.Date_application,
        p.IDPERSONNELS,
        Personnel = p.Personnel == null ? null : new { p.Personnel.Nom_Personnel, p.Personnel.Prenom_Personnel },
        p.Date_heure_debut,
        p.Date_heure_fin,
        p.Heure_coupure,
        p.Heure_nuit,
        p.Heure_liaison,
        p.Heure_Stanby,
        p.IDType_Statut,
        TypeStatut = p.TypeStatut == null ? null : new { p.TypeStatut.Libelle_generique },
        p.Nuitee,
        p.Remarque,
    });

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^\d{2}:\d{2}(:\d{2})?$")]
    private static partial Regex HhMm();
}

public class PointageController(PointageService service) : ApiControllerBase
{
    [HttpGet("pointage")]
    public Task<object> GetPointages([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetPointagesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    [HttpGet("pointage/statuts")]
    public Task<object> GetStatuts(CancellationToken ct) => service.GetStatutsAsync(ct);

    // from/to inclus
    [HttpGet("pointage/feuille")]
    public Task<object> GetFeuille([FromQuery] string? personnelId, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        service.GetFeuilleAsync(personnelId, from, to, ct);

    [HttpGet("pointage/{id}")]
    public Task<object> GetPointage(string id, CancellationToken ct) => service.GetPointageAsync(Params.ParseId(id), ct);

    [HttpPost("pointage")]
    public Task<IActionResult> CreatePointage([Body] CreatePointageDto dto, CancellationToken ct) => Created201(service.CreatePointageAsync(dto, ct));

    [HttpPatch("pointage/{id}")]
    public Task<object> UpdatePointage(string id, [Body] UpdatePointageDto dto, CancellationToken ct) =>
        service.UpdatePointageAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("pointage/{id}")]
    public Task DeletePointage(string id, CancellationToken ct) => service.DeletePointageAsync(Params.ParseId(id), ct);
}
