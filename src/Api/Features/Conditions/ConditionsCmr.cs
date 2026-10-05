using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Conditions;

// ===== SERVICE =====
// Conditions CMR d'un contrat (table conditions_cmr) : lignes « libellé +
// case à cocher » affichées dans la fiche contrat. Pas de lecture ici : les
// conditions d'un contrat sont renvoyées avec la fiche contrat
// (GET /contrats/:id).
public class ConditionCmrService(AppDbContext db)
{
    public async Task<object> CreateConditionCmrAsync(CreateConditionCmrDto dto, CancellationToken ct)
    {
        // IDUTILISATEURS_createur/modificateur ont un défaut DB de 0, qui viole
        // leur contrainte de clé étrangère quand ils sont omis — mis
        // explicitement à NULL.
        var conditionCmr = new ConditionCmr
        {
            IDCONTRATS = Dto.IdOrNull(dto.IDCONTRATS),
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = DateTime.UtcNow,
            Date_heure_modification = DateTime.UtcNow,
        };
        dto.Libelle.Apply(v => conditionCmr.Libelle = v);
        dto.Boite_a_cocher.ApplyInt(v => conditionCmr.Boite_a_cocher = v);
        db.ConditionCmr.Add(conditionCmr);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(conditionCmr.IDCONDITIONS_CMR, ct);
    }

    public async Task<object> UpdateConditionCmrAsync(long id, UpdateConditionCmrDto dto, CancellationToken ct)
    {
        var conditionCmr = await db.ConditionCmr.Where(c => c.IDCONDITIONS_CMR == id).FirstOrThrowAsync(ct);
        dto.Libelle.Apply(v => conditionCmr.Libelle = v);
        dto.Boite_a_cocher.ApplyInt(v => conditionCmr.Boite_a_cocher = v);
        conditionCmr.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ViewAsync(id, ct);
    }

    public Task DeleteConditionCmrAsync(long id, CancellationToken ct) =>
        db.ConditionCmr.Where(c => c.IDCONDITIONS_CMR == id).DeleteOrThrowAsync(ct);

    private Task<object> ViewAsync(long id, CancellationToken ct) =>
        db.ConditionCmr.Where(c => c.IDCONDITIONS_CMR == id)
            .Select(c => (object)new { c.IDCONDITIONS_CMR, c.Libelle, c.Boite_a_cocher })
            .FirstOrThrowAsync(ct);
}

// ===== CONTROLLER =====
public class ConditionCmrController(ConditionCmrService service) : ApiControllerBase
{
    // NestJS : POST conditions-cmr
    [HttpPost("conditions-cmr")]
    public Task<IActionResult> CreateConditionCmr([Body] CreateConditionCmrDto dto, CancellationToken ct) =>
        Created201(service.CreateConditionCmrAsync(dto, ct));

    // NestJS : PATCH conditions-cmr/:id
    [HttpPatch("conditions-cmr/{id}")]
    public Task<object> UpdateConditionCmr(string id, [Body] UpdateConditionCmrDto dto, CancellationToken ct) =>
        service.UpdateConditionCmrAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE conditions-cmr/:id
    [HttpDelete("conditions-cmr/{id}")]
    public Task DeleteConditionCmr(string id, CancellationToken ct) => service.DeleteConditionCmrAsync(Params.ParseId(id), ct);
}
