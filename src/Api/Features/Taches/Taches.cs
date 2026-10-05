using System.Text.RegularExpressions;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Taches;

// ===== SERVICE =====
// Alimente le bandeau « Tâches à faire » (frontend/src/layout/TasksPanel.tsx).
// Les tâches sont créées par l'ancienne application WinDev :
//   - Type 0 : tâche saisie à la main (pas de lien) ;
//   - Type 1 : échéance d'un document du personnel (Idlien = IDPERSONNELS) ;
//   - Type 2 : échéance d'un véhicule (Idlien = IDVEHICULES).
// Date_tache est la date d'alerte (quelques semaines avant l'échéance citée
// dans le libellé).
public class TacheService(AppDbContext db)
{
    // Tâches non traitées dont la date d'alerte est atteinte au jour `jusquAu`
    // (inclus), pour tous les utilisateurs, la plus ancienne en premier.
    public async Task<object> GetTachesAFaireAsync(DateTime jusquAu, CancellationToken ct)
    {
        var taches = await Select(db.Tache
                .Where(t => (t.Traite == 0 || t.Traite == null) && t.Date_tache <= jusquAu)
                .OrderBy(t => t.Date_tache).ThenBy(t => t.IDTACHES))
            .ToListAsync(ct);
        return new { taches };
    }

    public async Task<object> TraiterTacheAsync(long id, TraiterTacheDto dto, CancellationToken ct)
    {
        var tache = await db.Tache.Where(t => t.IDTACHES == id).FirstOrThrowAsync(ct);
        tache.Traite = 1;
        tache.Date_traitement = Dto.Date(dto.Date_traitement.Value).Date;
        tache.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await Select(db.Tache.Where(t => t.IDTACHES == id)).FirstOrThrowAsync(ct);
    }

    // Décale la date d'alerte sans clôturer la tâche : elle sort du bandeau
    // jusqu'à cette date.
    public async Task<object> ReprogrammerTacheAsync(long id, ReprogrammerTacheDto dto, CancellationToken ct)
    {
        var tache = await db.Tache.Where(t => t.IDTACHES == id).FirstOrThrowAsync(ct);
        tache.Date_tache = Dto.Date(dto.Date_tache.Value).Date;
        tache.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await Select(db.Tache.Where(t => t.IDTACHES == id)).FirstOrThrowAsync(ct);
    }

    private static IQueryable<object> Select(IQueryable<Tache> query) =>
        query.Select(t => new { t.IDTACHES, t.Type, t.Libelle, t.Date_tache, t.Idlien });
}

// ===== CONTROLLER =====
public partial class TacheController(TacheService service) : ApiControllerBase
{
    // NestJS : GET taches/a-faire
    // ?jusquAu=AAAA-MM-JJ (obligatoire) : date du jour côté navigateur.
    [HttpGet("taches/a-faire")]
    public Task<object> GetTachesAFaire([FromQuery] string? jusquAu, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(jusquAu) || !Day().IsMatch(jusquAu) || !JsDate.TryParse(jusquAu, out var date))
        {
            throw new BadRequestException("Paramètre « jusquAu » invalide (AAAA-MM-JJ attendu).");
        }
        return service.GetTachesAFaireAsync(date, ct);
    }

    // NestJS : PATCH taches/:id/traiter
    [HttpPatch("taches/{id}/traiter")]
    public Task<object> TraiterTache(string id, [Body] TraiterTacheDto dto, CancellationToken ct) =>
        service.TraiterTacheAsync(Params.ParseId(id), dto, ct);

    // NestJS : PATCH taches/:id/reprogrammer
    [HttpPatch("taches/{id}/reprogrammer")]
    public Task<object> ReprogrammerTache(string id, [Body] ReprogrammerTacheDto dto, CancellationToken ct) =>
        service.ReprogrammerTacheAsync(Params.ParseId(id), dto, ct);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex Day();
}
