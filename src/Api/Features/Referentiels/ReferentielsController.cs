using Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Api.Features.Referentiels;

[ApiController]
public class ReferentielsController(ReferentielsService service) : ControllerBase
{
    [HttpGet("pays")]
    public Task<object> GetPays(CancellationToken ct) => service.GetPaysAsync(ct);

    // cp obligatoire, jamais la liste complète
    [HttpGet("villes")]
    public async Task<object> GetVilles([FromQuery] string? cp, CancellationToken ct) =>
        string.IsNullOrEmpty(cp) ? new { villes = Array.Empty<object>() } : await service.SearchVillesAsync(cp, ct);

    [HttpGet("types-facture")]
    public Task<object> GetTypesFacture(CancellationToken ct) => service.GetTypesFactureAsync(ct);

    // search : code ou description
    [HttpGet("dechets")]
    public Task<object> GetDechets([FromQuery] string? search, CancellationToken ct) => service.GetDechetsAsync(search, ct);

    // societeId obligatoire
    [HttpGet("messages")]
    public Task<object> GetMessages([FromQuery] string? societeId, [FromQuery] string? search, CancellationToken ct) =>
        service.GetMessagesBySocieteAsync(Params.ParseId(societeId), search, ct);
}
