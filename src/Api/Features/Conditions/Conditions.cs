using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Conditions;

// ===== SERVICE =====
// Conditions d'exécution (table conditions_execution).
public class ConditionService(AppDbContext db)
{
    // page/pageSize optionnels : omis, toute la table. Fournis, la requête est
    // découpée et `total` (nombre total de lignes, pas juste celles de la
    // page) est renvoyé à côté pour que le frontend calcule le nombre de pages.
    public async Task<object> GetConditionsAsync(int? page, int? pageSize, string? search, CancellationToken ct)
    {
        var query = db.ConditionExecution.WhereSearch(search, p => c => EF.Functions.Like(c.Libelle, p));
        var conditions = await Select(query.OrderBy(c => c.IDCONDITIONS_EXECUTION).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { conditions, total };
    }

    // Pas de select « détail » séparé : la liste contient déjà tous les champs
    // métier. GET /:id existe pour garder une API uniforme avec les autres
    // modules.
    public Task<object> GetConditionAsync(long id, CancellationToken ct) =>
        Select(db.ConditionExecution.Where(c => c.IDCONDITIONS_EXECUTION == id)).FirstOrThrowAsync(ct);

    public async Task<object> CreateConditionAsync(CreateConditionDto dto, CancellationToken ct)
    {
        var condition = new ConditionExecution();
        Assign(condition, dto);
        db.ConditionExecution.Add(condition);
        await db.SaveChangesAsync(ct);
        return await GetConditionAsync(condition.IDCONDITIONS_EXECUTION, ct);
    }

    public async Task<object> UpdateConditionAsync(long id, UpdateConditionDto dto, CancellationToken ct)
    {
        var condition = await db.ConditionExecution.Where(c => c.IDCONDITIONS_EXECUTION == id).FirstOrThrowAsync(ct);
        Assign(condition, dto);
        await db.SaveChangesAsync(ct);
        return await GetConditionAsync(id, ct);
    }

    public Task DeleteConditionAsync(long id, CancellationToken ct) =>
        db.ConditionExecution.Where(c => c.IDCONDITIONS_EXECUTION == id).DeleteOrThrowAsync(ct);

    private static void Assign(ConditionExecution c, CreateConditionDto dto)
    {
        dto.Type_Prestation.ApplyInt(v => c.Type_Prestation = v);
        dto.CMR_or_FDR.ApplyInt(v => c.CMR_or_FDR = v);
        dto.Libelle.Apply(v => c.Libelle = v);
    }

    private static IQueryable<object> Select(IQueryable<ConditionExecution> query) =>
        query.Select(c => new { c.IDCONDITIONS_EXECUTION, c.Type_Prestation, c.CMR_or_FDR, c.Libelle });
}

// ===== CONTROLLER =====
public class ConditionController(ConditionService service) : ApiControllerBase
{
    // NestJS : GET conditions
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("conditions")]
    public Task<object> GetConditions([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, CancellationToken ct) =>
        service.GetConditionsAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, ct);

    // NestJS : POST conditions
    [HttpPost("conditions")]
    public Task<IActionResult> CreateCondition([Body] CreateConditionDto dto, CancellationToken ct) =>
        Created201(service.CreateConditionAsync(dto, ct));

    // NestJS : PATCH conditions/:id
    [HttpPatch("conditions/{id}")]
    public Task<object> UpdateCondition(string id, [Body] UpdateConditionDto dto, CancellationToken ct) =>
        service.UpdateConditionAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE conditions/:id
    [HttpDelete("conditions/{id}")]
    public Task DeleteCondition(string id, CancellationToken ct) => service.DeleteConditionAsync(Params.ParseId(id), ct);
}
