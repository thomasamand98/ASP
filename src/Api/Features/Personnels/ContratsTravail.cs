using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Personnels;

// ===== SERVICE =====
// Contrats de travail (table contrats_travail) d'un personnel, chacun avec
// l'historique de ses taux horaires (taux_horaire_contrat_travail) : un taux
// s'applique à partir de sa Date_application, le plus récent passé étant en
// vigueur (voir PointageService.GetFeuilleAsync).
public class ContratTravailService(AppDbContext db)
{
    // Plus récent en premier.
    public async Task<object> GetContratsAsync(long personnelId, CancellationToken ct)
    {
        var ids = await db.ContratTravail.Where(c => c.IDPERSONNELS == personnelId)
            .OrderByDescending(c => c.Date_debut).ThenByDescending(c => c.IDCONTRATS_TRAVAIL)
            .Select(c => c.IDCONTRATS_TRAVAIL)
            .ToListAsync(ct);
        return new { contrats = await ViewAsync(ids, ct) };
    }

    public async Task<object> CreateContratAsync(long personnelId, ContratTravailDto dto, CancellationToken ct)
    {
        // IDSOCIETES/IDUTILISATEURS_* ont un défaut DB de 0, qui viole leur
        // contrainte de clé étrangère — mis à NULL.
        var contrat = new ContratTravail
        {
            IDPERSONNELS = personnelId,
            IDSOCIETES = null,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = DateTime.UtcNow,
        };
        Assign(contrat, dto);
        foreach (var taux in dto.Taux.OrDefault ?? []) contrat.TauxHoraireContratTravails.Add(NewTaux(taux));
        db.ContratTravail.Add(contrat);
        await db.SaveChangesAsync(ct);
        return (await ViewAsync([contrat.IDCONTRATS_TRAVAIL], ct))[0];
    }

    // Les taux envoyés remplacent ceux du contrat (voir ContratTravailDto.Taux).
    public async Task<object> UpdateContratAsync(long id, ContratTravailDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (dto.Taux.IsSet)
        {
            var existing = await db.TauxHoraireContratTravail.Where(t => t.IDCONTRATS_TRAVAIL == id).Select(t => t.IDTAUX_HORAIRE).ToListAsync(ct);
            var kept = dto.Taux.Value.Select(t => t.IDTAUX_HORAIRE.OrDefault).Where(tauxId => !string.IsNullOrEmpty(tauxId)).Select(tauxId => Dto.Id(tauxId!)).ToHashSet();
            await DeleteTauxAsync(existing.Where(tauxId => !kept.Contains(tauxId)).ToList(), ct);
            foreach (var taux in dto.Taux.Value)
            {
                if (!string.IsNullOrEmpty(taux.IDTAUX_HORAIRE.OrDefault))
                {
                    var tauxId = Dto.Id(taux.IDTAUX_HORAIRE.Value);
                    var current = await db.TauxHoraireContratTravail.Where(t => t.IDTAUX_HORAIRE == tauxId && t.IDCONTRATS_TRAVAIL == id).FirstOrThrowAsync(ct);
                    AssignTaux(current, taux);
                    current.Date_heure_modification = DateTime.UtcNow;
                }
                else
                {
                    var created = NewTaux(taux);
                    created.IDCONTRATS_TRAVAIL = id;
                    db.TauxHoraireContratTravail.Add(created);
                }
                await db.SaveChangesAsync(ct);
            }
        }
        // Les champs du contrat sont validés avant sa lecture, comme dans le
        // backend d'origine (date invalide : 400 avant 404).
        Assign(new ContratTravail(), dto);
        var contrat = await db.ContratTravail.Where(c => c.IDCONTRATS_TRAVAIL == id).FirstOrThrowAsync(ct);
        Assign(contrat, dto);
        contrat.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ViewAsync([id], ct))[0];
    }

    public async Task DeleteContratAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var taux = await db.TauxHoraireContratTravail.Where(t => t.IDCONTRATS_TRAVAIL == id).Select(t => t.IDTAUX_HORAIRE).ToListAsync(ct);
        await DeleteTauxAsync(taux, ct);
        await db.ContratTravail.Where(c => c.IDCONTRATS_TRAVAIL == id).DeleteOrThrowAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Un taux déjà utilisé par un résumé de pointage n'est pas supprimé : la
    // paie calculée avec lui en dépend.
    private async Task DeleteTauxAsync(List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var keys = ids.Cast<long?>().ToList();
        var used = await db.PointageResume.CountAsync(r => keys.Contains(r.IDTAUX_HORAIRE), ct);
        if (used > 0) throw new ConflictException("Un taux horaire déjà utilisé par des pointages ne peut pas être supprimé.");
        await db.TauxHoraireContratTravail.Where(t => ids.Contains(t.IDTAUX_HORAIRE)).ExecuteDeleteAsync(ct);
    }

    // Champs non envoyés laissés tels quels ; textes vides → NULL.
    private static void Assign(ContratTravail c, ContratTravailDto dto)
    {
        static string? TrimOrNull(string value) => value.Trim().Length > 0 ? value.Trim() : null;

        dto.TypeContrat.Apply(v => c.TypeContrat = TrimOrNull(v));
        dto.Qualification.Apply(v => c.Qualification = TrimOrNull(v));
        dto.Date_debut.Apply(v => c.Date_debut = Dto.ToDay(v, "Date de début"));
        dto.Date_fin.Apply(v => c.Date_fin = Dto.ToDay(v, "Date de fin"));
        dto.Routier.Apply(v => c.Routier = v != 0 ? 1 : 0);
        dto.Manutention.Apply(v => c.Manutention = v != 0 ? 1 : 0);
        dto.Atelier.Apply(v => c.Atelier = v != 0 ? 1 : 0);
    }

    private static TauxHoraireContratTravail NewTaux(TauxHoraireDto dto)
    {
        var taux = new TauxHoraireContratTravail { Date_heure_creation = DateTime.UtcNow, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        AssignTaux(taux, dto);
        return taux;
    }

    // Tous les taux sont réécrits : un taux non envoyé vaut 0.
    private static void AssignTaux(TauxHoraireContratTravail t, TauxHoraireDto dto)
    {
        t.Taux_horaire = ToRate(dto.Taux_horaire, nameof(dto.Taux_horaire));
        t.Taux_horaire_supplementaire = ToRate(dto.Taux_horaire_supplementaire, nameof(dto.Taux_horaire_supplementaire));
        t.Taux_horaire_nuitee = ToRate(dto.Taux_horaire_nuitee, nameof(dto.Taux_horaire_nuitee));
        t.Taux_nuitee = ToRate(dto.Taux_nuitee, nameof(dto.Taux_nuitee));
        t.Taux_samedi = ToRate(dto.Taux_samedi, nameof(dto.Taux_samedi));
        t.Taux_cheque_repas = ToRate(dto.Taux_cheque_repas, nameof(dto.Taux_cheque_repas));
        t.Taux_stanby = ToRate(dto.Taux_stanby, nameof(dto.Taux_stanby));
        t.Date_application = Dto.ToDay(dto.Date_application.OrDefault, "Date d’application");
    }

    private static decimal ToRate(Opt<object> raw, string field)
    {
        if (!raw.IsSet || raw.Value is "") return 0;
        var value = raw.Value is string text ? JsNumber.Parse(ReplaceFirstComma(text)) : (double)raw.Value;
        if (!double.IsFinite(value) || value < 0) throw new BadRequestException($"Montant invalide pour « {field} ».");
        return (decimal)value;
    }

    private static string ReplaceFirstComma(string text)
    {
        var index = text.IndexOf(',');
        return index < 0 ? text : $"{text[..index]}.{text[(index + 1)..]}";
    }

    // Contrats demandés, dans l'ordre des ids, avec leur société et leurs
    // taux (le plus récent en premier).
    private async Task<List<object>> ViewAsync(List<long> ids, CancellationToken ct)
    {
        var keys = ids.Cast<long?>().ToList();
        var contrats = await db.ContratTravail.Where(c => ids.Contains(c.IDCONTRATS_TRAVAIL))
            .Select(c => new
            {
                c.IDCONTRATS_TRAVAIL,
                c.TypeContrat,
                c.Qualification,
                c.Date_debut,
                c.Date_fin,
                c.Routier,
                c.Manutention,
                c.Atelier,
                Societe = c.Societe == null ? null : new { c.Societe.IDSOCIETES, c.Societe.Nom_societe },
            })
            .ToDictionaryAsync(c => c.IDCONTRATS_TRAVAIL, ct);
        var taux = (await db.TauxHoraireContratTravail.Where(t => keys.Contains(t.IDCONTRATS_TRAVAIL))
                .OrderByDescending(t => t.Date_application).ThenByDescending(t => t.IDTAUX_HORAIRE)
                .Select(t => new
                {
                    t.IDCONTRATS_TRAVAIL,
                    Taux = new
                    {
                        t.IDTAUX_HORAIRE,
                        t.Date_application,
                        t.Taux_horaire,
                        t.Taux_horaire_supplementaire,
                        t.Taux_horaire_nuitee,
                        t.Taux_nuitee,
                        t.Taux_samedi,
                        t.Taux_cheque_repas,
                        t.Taux_stanby,
                    },
                })
                .ToListAsync(ct))
            .ToLookup(t => t.IDCONTRATS_TRAVAIL!.Value, t => t.Taux);

        return ids.Where(contrats.ContainsKey).Select(id =>
        {
            var c = contrats[id];
            return (object)new
            {
                c.IDCONTRATS_TRAVAIL,
                c.TypeContrat,
                c.Qualification,
                c.Date_debut,
                c.Date_fin,
                c.Routier,
                c.Manutention,
                c.Atelier,
                c.Societe,
                TauxHoraireContratTravails = taux[id],
            };
        }).ToList();
    }
}

// ===== CONTROLLER =====
// Contrats de travail d'un personnel et leurs taux horaires (onglet Contrat
// de la fiche personnel).
public class ContratTravailController(ContratTravailService service) : ApiControllerBase
{
    // NestJS : GET personnel/:id/contrats-travail
    [HttpGet("personnel/{id}/contrats-travail")]
    public Task<object> GetContrats(string id, CancellationToken ct) => service.GetContratsAsync(Params.ParseId(id), ct);

    // NestJS : POST personnel/:id/contrats-travail
    [HttpPost("personnel/{id}/contrats-travail")]
    public Task<IActionResult> CreateContrat(string id, [Body] ContratTravailDto dto, CancellationToken ct) =>
        Created201(service.CreateContratAsync(Params.ParseId(id), dto, ct));

    // NestJS : PATCH contrats-travail/:id
    [HttpPatch("contrats-travail/{id}")]
    public Task<object> UpdateContrat(string id, [Body] ContratTravailDto dto, CancellationToken ct) =>
        service.UpdateContratAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE contrats-travail/:id
    [HttpDelete("contrats-travail/{id}")]
    public Task DeleteContrat(string id, CancellationToken ct) => service.DeleteContratAsync(Params.ParseId(id), ct);
}
