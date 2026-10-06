using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Attelages;

// ===== SERVICE =====
// Attelages de référence (table attelages_reference) : le couple tracteur +
// remorque habituel de chaque chauffeur — celui repris par défaut au
// planning. La table `attelage` (historique daté) n'est plus exposée : elle
// ne contient que d'anciennes lignes de test.
// Tracteur/remorque n'ont pas de relation (colonnes sans clé étrangère) :
// résolus à part par WithVehiculesAsync.
public class AttelageService(AppDbContext db)
{
    private sealed record Row(long IDATTELAGE_REFERENCE, long? IDCHAUFFEUR, string? ChauffeurNom, bool HasChauffeur, long? IDTRACTEUR, long? IDREMORQUE, long? IDSOCIETES, string? SocieteNom, bool HasSociete);

    // Projection (voir Common/Projection.cs) : pour chaque table source, les
    // lignes de cette table liées aux ids sélectionnés.
    private static readonly ProjectionMap<AttelageReference> Projections = new()
    {
        ["chauffeurs"] = ids => a => a.IDCHAUFFEUR != null && ids.Contains(a.IDCHAUFFEUR.Value),
        ["personnel"] = ids => a => a.Chauffeur!.IDPERSONNELS != null && ids.Contains(a.Chauffeur.IDPERSONNELS.Value),
        ["vehicules"] = ids => a => (a.IDTRACTEUR != null && ids.Contains(a.IDTRACTEUR.Value)) || (a.IDREMORQUE != null && ids.Contains(a.IDREMORQUE.Value)),
    };

    // Un id 0 (défaut WinDev) équivaut à « aucun lien ».
    private static bool IsId(long? value) => value is > 0;

    // page/pageSize optionnels : omis, toute la table. La recherche porte
    // aussi sur les véhicules, via leurs ids (pas de relation).
    public async Task<object> GetAttelagesAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = (await SearchAsync(db.AttelageReference, search, ct)).WhereProjection(Projections, projection);
        var rows = await Select(query.OrderBy(a => a.Chauffeur!.Nom_chauffeur).ThenBy(a => a.IDATTELAGE_REFERENCE).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { attelages = await WithVehiculesAsync(rows, ct), total };
    }

    public async Task<object> CreateAttelageAsync(CreateAttelageDto dto, CancellationToken ct)
    {
        // Les IDs ont un défaut DB de 0, qui viole les clés étrangères quand
        // ils sont omis — mis explicitement à NULL.
        var attelage = new AttelageReference
        {
            IDCHAUFFEUR = null,
            IDTRACTEUR = null,
            IDREMORQUE = null,
            IDSOCIETES = null,
            IDPERSONNELS = null,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = DateTime.UtcNow,
        };
        Assign(attelage, dto);
        db.AttelageReference.Add(attelage);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(attelage.IDATTELAGE_REFERENCE, ct);
    }

    public async Task<object> UpdateAttelageAsync(long id, UpdateAttelageDto dto, CancellationToken ct)
    {
        var attelage = await db.AttelageReference.Where(a => a.IDATTELAGE_REFERENCE == id).FirstOrThrowAsync(ct);
        Assign(attelage, dto);
        attelage.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ViewAsync(id, ct);
    }

    public Task DeleteAttelageAsync(long id, CancellationToken ct) =>
        db.AttelageReference.Where(a => a.IDATTELAGE_REFERENCE == id).DeleteOrThrowAsync(ct);

    // Seuls les IDs sont convertis ; "" retire le lien (NULL : 0 violerait la
    // clé étrangère).
    private static void Assign(AttelageReference a, CreateAttelageDto dto)
    {
        Dto.OptionalId(dto.IDCHAUFFEUR).Apply(v => a.IDCHAUFFEUR = v);
        Dto.OptionalId(dto.IDTRACTEUR).Apply(v => a.IDTRACTEUR = v);
        Dto.OptionalId(dto.IDREMORQUE).Apply(v => a.IDREMORQUE = v);
        Dto.OptionalId(dto.IDSOCIETES).Apply(v => a.IDSOCIETES = v);
    }

    private static IQueryable<Row> Select(IQueryable<AttelageReference> query) => query.Select(a => new Row(
        a.IDATTELAGE_REFERENCE, a.IDCHAUFFEUR, a.Chauffeur!.Nom_chauffeur, a.Chauffeur != null, a.IDTRACTEUR, a.IDREMORQUE, a.IDSOCIETES, a.Societe!.Nom_societe, a.Societe != null));

    private async Task<object> ViewAsync(long id, CancellationToken ct) =>
        (await WithVehiculesAsync([await Select(db.AttelageReference.Where(a => a.IDATTELAGE_REFERENCE == id)).FirstOrThrowAsync(ct)], ct))[0];

    // Ajoute Tracteur/Remorque (marque, modèle, immatriculation) à chaque
    // ligne, en une seule requête sur les véhicules.
    private async Task<List<object>> WithVehiculesAsync(List<Row> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(r => new[] { r.IDTRACTEUR, r.IDREMORQUE }).Where(IsId).Select(id => id!.Value).Distinct().ToList();
        var vehicules = ids.Count > 0
            ? await db.Vehicule.Where(v => ids.Contains(v.IDVEHICULES)).Select(v => new { v.IDVEHICULES, v.Marque, v.Modele, v.Num_immat }).ToListAsync(ct)
            : [];
        var byId = vehicules.ToDictionary(v => v.IDVEHICULES, v => (object)new { v.Marque, v.Modele, v.Num_immat });
        return rows.Select(r => (object)new
        {
            r.IDATTELAGE_REFERENCE,
            r.IDCHAUFFEUR,
            Chauffeur = r.HasChauffeur ? new { Nom_chauffeur = r.ChauffeurNom } : null,
            r.IDTRACTEUR,
            r.IDREMORQUE,
            r.IDSOCIETES,
            Societe = r.HasSociete ? new { Nom_societe = r.SocieteNom } : null,
            Tracteur = IsId(r.IDTRACTEUR) ? byId.GetValueOrDefault(r.IDTRACTEUR!.Value) : null,
            Remorque = IsId(r.IDREMORQUE) ? byId.GetValueOrDefault(r.IDREMORQUE!.Value) : null,
        }).ToList();
    }

    // Recherche sur le chauffeur, la société et les véhicules : ces derniers
    // sans relation, les véhicules correspondant à chaque mot sont cherchés
    // d'abord, puis filtrés par id.
    private async Task<IQueryable<AttelageReference>> SearchAsync(IQueryable<AttelageReference> query, string? search, CancellationToken ct)
    {
        foreach (var term in Search.Terms(search))
        {
            var p = Search.Pattern(term);
            var ids = (await db.Vehicule
                    .Where(v => EF.Functions.Like(v.Marque, p) || EF.Functions.Like(v.Modele, p) || EF.Functions.Like(v.Num_immat, p))
                    .Select(v => v.IDVEHICULES)
                    .ToListAsync(ct))
                .Cast<long?>().ToList();
            query = query.Where(a =>
                EF.Functions.Like(a.Chauffeur!.Nom_chauffeur, p) || EF.Functions.Like(a.Societe!.Nom_societe, p) || ids.Contains(a.IDTRACTEUR) || ids.Contains(a.IDREMORQUE));
        }
        return query;
    }
}

// ===== CONTROLLER =====
public class AttelageController(AttelageService service) : ApiControllerBase
{
    // NestJS : GET attelages
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("attelages")]
    public Task<object> GetAttelages([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetAttelagesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    // NestJS : POST attelages
    [HttpPost("attelages")]
    public Task<IActionResult> CreateAttelage([Body] CreateAttelageDto dto, CancellationToken ct) => Created201(service.CreateAttelageAsync(dto, ct));

    // NestJS : PATCH attelages/:id
    [HttpPatch("attelages/{id}")]
    public Task<object> UpdateAttelage(string id, [Body] UpdateAttelageDto dto, CancellationToken ct) =>
        service.UpdateAttelageAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE attelages/:id
    [HttpDelete("attelages/{id}")]
    public Task DeleteAttelage(string id, CancellationToken ct) => service.DeleteAttelageAsync(Params.ParseId(id), ct);
}
