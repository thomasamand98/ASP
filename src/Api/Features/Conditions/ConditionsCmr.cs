using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Conditions;

// pas de GET, renvoyées avec la fiche contrat
public class ConditionCmrService(AppDbContext db)
{
    public async Task<object> CreateConditionCmrAsync(CreateConditionCmrDto dto, CancellationToken ct)
    {
        // défaut DB à 0, casse la FK
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

public class ConditionCmrController(ConditionCmrService service) : ApiControllerBase
{
    [HttpPost("conditions-cmr")]
    public Task<IActionResult> CreateConditionCmr([Body] CreateConditionCmrDto dto, CancellationToken ct) =>
        Created201(service.CreateConditionCmrAsync(dto, ct));

    [HttpPatch("conditions-cmr/{id}")]
    public Task<object> UpdateConditionCmr(string id, [Body] UpdateConditionCmrDto dto, CancellationToken ct) =>
        service.UpdateConditionCmrAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("conditions-cmr/{id}")]
    public Task DeleteConditionCmr(string id, CancellationToken ct) => service.DeleteConditionCmrAsync(Params.ParseId(id), ct);
}
