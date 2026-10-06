using Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Referentiels;

// Reçoit les requêtes HTTP du front et délègue à ReferentielsService.
[ApiController]
public class ReferentielsController(ReferentielsService service) : ControllerBase
{
    // NestJS : PaysController.getPays — GET pays
    [HttpGet("pays")]
    public Task<object> GetPays(CancellationToken ct) => service.GetPaysAsync(ct);

    // NestJS : VilleController.getVilles — GET villes
    // ?cp=7390 — requis : pas de liste complète, voir ReferentielsService.
    [HttpGet("villes")]
    public async Task<object> GetVilles([FromQuery] string? cp, CancellationToken ct) =>
        string.IsNullOrEmpty(cp) ? new { villes = Array.Empty<object>() } : await service.SearchVillesAsync(cp, ct);

    // NestJS : TypeFactureController.getTypesFacture — GET types-facture
    [HttpGet("types-facture")]
    public Task<object> GetTypesFacture(CancellationToken ct) => service.GetTypesFactureAsync(ct);

    // NestJS : DechetController.getDechets — GET dechets
    // ?search= : filtre sur le code et la description.
    [HttpGet("dechets")]
    public Task<object> GetDechets([FromQuery] string? search, CancellationToken ct) => service.GetDechetsAsync(search, ct);

    // NestJS : MessageController.getMessages — GET messages
    // ?societeId=X (obligatoire) : liste des messages envoyés liés à cette
    // société.
    [HttpGet("messages")]
    public Task<object> GetMessages([FromQuery] string? societeId, [FromQuery] string? search, CancellationToken ct) =>
        service.GetMessagesBySocieteAsync(Params.ParseId(societeId), search, ct);
}
