using System.Text.RegularExpressions;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Pointages;

// ===== SERVICE =====
public partial class PointageService(AppDbContext db)
{
    // Au-delà, la feuille devient illisible (même ordre de grandeur qu'un mois
    // ou deux).
    private const int MaxFeuilleDays = 93;

    // Projection (voir Common/Projection.cs) : pour chaque table source, les
    // lignes de cette table liées aux ids sélectionnés.
    private static readonly ProjectionMap<Pointage> Projections = new()
    {
        ["personnel"] = ids => p => p.IDPERSONNELS != null && ids.Contains(p.IDPERSONNELS.Value),
    };

    // page/pageSize optionnels : omis, toute la table.
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

    // Tous les statuts, archivés compris : le formulaire masque les archivés
    // sauf celui déjà porté par le pointage modifié, pour ne pas le perdre.
    public async Task<object> GetStatutsAsync(CancellationToken ct)
    {
        var statuts = await db.TypeStatut.OrderBy(s => s.Ordre).ThenBy(s => s.Libelle_generique)
            .Select(s => new { s.IDType_Statut, s.Libelle_generique, s.CouleurStatut, s.Statut_de_travail, s.Archive })
            .ToListAsync(ct);
        return new { statuts };
    }

    // Pointages d'un salarié du jour `fromRaw` au jour `toRaw` inclus
    // (Date_application est une colonne DATE, représentée à minuit UTC), et
    // le taux horaire en vigueur à la fin de la période : le plus récent de
    // ses contrats de travail dont la date d'application est passée.
    public async Task<object> GetFeuilleAsync(string? personnelIdRaw, string? fromRaw, string? toRaw, CancellationToken ct)
    {
        if (personnelIdRaw is null || !Digits().IsMatch(personnelIdRaw)) throw new BadRequestException("Salarié invalide.");
        var from = Params.ParseDay(fromRaw, "from");
        var to = Params.ParseDay(toRaw, "to");
        if (to < from) throw new BadRequestException("La fin de la période doit être après son début.");
        if ((to - from).TotalDays >= MaxFeuilleDays) throw new BadRequestException($"La période ne peut pas dépasser {MaxFeuilleDays} jours.");
        var personnelId = Dto.Id(personnelIdRaw);

        // Fiche complète, plus la couleur et le caractère « travaillé » du
        // statut pour l'affichage.
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

    // Détail : ajoute les pauses, absentes du tableau mais scripturables.
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
        // IDPERSONNELS/IDType_Statut/IDUTILISATEURS_* ont un défaut DB de 0,
        // qui viole leur contrainte de clé étrangère quand ils sont omis — mis
        // explicitement à NULL. Heure_Stanby : 00:00 par défaut.
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
        // Les dates et heures sont validées avant la lecture (400 avant 404).
        Assign(new Pointage(), dto);
        var pointage = await db.Pointage.Where(p => p.IDPOINTAGES == id).FirstOrThrowAsync(ct);
        Assign(pointage, dto);
        await db.SaveChangesAsync(ct);
        return await ListSelect(db.Pointage.Where(p => p.IDPOINTAGES == id)).FirstOrThrowAsync(ct);
    }

    public Task DeletePointageAsync(long id, CancellationToken ct) => db.Pointage.Where(p => p.IDPOINTAGES == id).DeleteOrThrowAsync(ct);

    // Les IDs et les dates / heures sont convertis ici ; le formulaire envoie :
    //   - Date_application : un jour « AAAA-MM-JJ » (colonne DATE → minuit UTC) ;
    //   - Date_heure_debut / fin : un instant ISO ;
    //   - Heure_* / pauses : « HH:MM », ou un datetime du 01/01/1970 (colonne
    //     TIME).
    // Un champ vide ("") efface la valeur (NULL). Heure_Stanby n'est pas
    // nullable : vide, il vaut 00:00.
    private static void Assign(Pointage p, CreatePointageDto dto)
    {
        // Même ordre de validation que le backend d'origine.
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
        // NOT NULL : vidé, il revient à 00:00 plutôt que de faire échouer l'écriture.
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

// ===== CONTROLLER =====
public class PointageController(PointageService service) : ApiControllerBase
{
    // NestJS : GET pointage
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("pointage")]
    public Task<object> GetPointages([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetPointagesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    // NestJS : GET pointage/statuts
    // Liste des statuts de pointage (table types_statut) pour le sélecteur du
    // formulaire.
    [HttpGet("pointage/statuts")]
    public Task<object> GetStatuts(CancellationToken ct) => service.GetStatutsAsync(ct);

    // NestJS : GET pointage/feuille
    // Feuille de pointage d'un salarié : ses pointages du jour `from` au jour
    // `to` (inclus, AAAA-MM-JJ) et ses taux horaires.
    [HttpGet("pointage/feuille")]
    public Task<object> GetFeuille([FromQuery] string? personnelId, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        service.GetFeuilleAsync(personnelId, from, to, ct);

    // NestJS : GET pointage/:id
    [HttpGet("pointage/{id}")]
    public Task<object> GetPointage(string id, CancellationToken ct) => service.GetPointageAsync(Params.ParseId(id), ct);

    // NestJS : POST pointage
    [HttpPost("pointage")]
    public Task<IActionResult> CreatePointage([Body] CreatePointageDto dto, CancellationToken ct) => Created201(service.CreatePointageAsync(dto, ct));

    // NestJS : PATCH pointage/:id
    [HttpPatch("pointage/{id}")]
    public Task<object> UpdatePointage(string id, [Body] UpdatePointageDto dto, CancellationToken ct) =>
        service.UpdatePointageAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE pointage/:id
    [HttpDelete("pointage/{id}")]
    public Task DeletePointage(string id, CancellationToken ct) => service.DeletePointageAsync(Params.ParseId(id), ct);
}
