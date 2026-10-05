using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Vehicules;

// ===== SERVICE =====
public class VehiculeService(AppDbContext db)
{
    // Projection (voir Common/Projection.cs). Depuis « attelages » : voir
    // AttelageProjectionIdsAsync (requête préalable, pas de relation vers
    // attelages_reference).
    private static readonly ProjectionMap<Vehicule> Projections = new()
    {
        ["societes"] = ids => v => v.IDSOCIETES != null && ids.Contains(v.IDSOCIETES.Value),
    };

    // page/pageSize optionnels : omis, toute la table.
    public async Task<object> GetVehiculesAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Vehicule.WhereSearch(search, p => v =>
            EF.Functions.Like(v.Num_immat, p) || EF.Functions.Like(v.Marque, p) || EF.Functions.Like(v.Modele, p) || EF.Functions.Like(v.Societe!.Nom_societe, p)
            || EF.Functions.Like(v.Num_police_assurance, p) || EF.Functions.Like(v.Num_chassis, p) || EF.Functions.Like(v.Num_licence_transport, p));
        if (projection?.Via == "attelages")
        {
            var vehiculeIds = await AttelageProjectionIdsAsync(projection.Ids, ct);
            query = query.Where(v => vehiculeIds.Contains(v.IDVEHICULES));
        }
        else
        {
            query = query.WhereProjection(Projections, projection);
        }

        var vehicules = await query.OrderBy(v => v.IDVEHICULES).Paginate(page, pageSize)
            .Select(v => new
            {
                v.IDVEHICULES,
                v.Num_immat,
                v.Type,
                v.Marque,
                v.Modele,
                v.IDSOCIETES,
                Societe = v.Societe == null ? null : new { v.Societe.Nom_societe },
                v.Num_police_assurance,
                v.Num_chassis,
                v.Date_validite_assurance,
                v.Num_licence_transport,
                v.Date_validite_licence,
                v.Date_inspection_auto,
                v.Date_validite_tachygeaphe,
            })
            .ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { vehicules, total };
    }

    // Projection depuis des attelages de référence : leurs tracteurs et
    // remorques. IDTRACTEUR/IDREMORQUE n'ont pas de relation, les ids sont
    // donc lus d'abord.
    private async Task<List<long>> AttelageProjectionIdsAsync(long[] ids, CancellationToken ct)
    {
        var attelages = await db.AttelageReference.Where(a => ids.Contains(a.IDATTELAGE_REFERENCE)).Select(a => new { a.IDTRACTEUR, a.IDREMORQUE }).ToListAsync(ct);
        return attelages.SelectMany(a => new[] { a.IDTRACTEUR, a.IDREMORQUE }).Where(id => id is > 0).Select(id => id!.Value).ToList();
    }

    // Détail : ajoute les champs scripturables absents de la liste.
    public Task<object> GetVehiculeAsync(long id, CancellationToken ct) =>
        db.Vehicule.Where(v => v.IDVEHICULES == id)
            .Select(v => (object)new
            {
                v.IDVEHICULES,
                v.Num_immat,
                v.Type,
                v.Marque,
                v.Modele,
                v.IDSOCIETES,
                Societe = v.Societe == null ? null : new { v.Societe.Nom_societe },
                v.Num_police_assurance,
                v.Num_chassis,
                v.Date_validite_assurance,
                v.Num_licence_transport,
                v.Date_validite_licence,
                v.Date_inspection_auto,
                v.Date_validite_tachygeaphe,
                v.Date_modification_licence,
                v.Date_radiation_immatriculation,
                v.Date_vente,
                v.Date_premiere_mise_en_circulation,
                v.Avec_compresseur,
            })
            .FirstOrThrowAsync(ct);

    public async Task<object> CreateVehiculeAsync(CreateVehiculeDto dto, CancellationToken ct)
    {
        // IDSOCIETES/IDUTILISATEURS_* ont un défaut DB de 0, qui viole leur
        // contrainte de clé étrangère quand ils sont omis — mis explicitement
        // à NULL.
        var vehicule = new Vehicule { IDSOCIETES = null, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        Assign(vehicule, dto);
        db.Vehicule.Add(vehicule);
        await db.SaveChangesAsync(ct);
        return await ListViewAsync(vehicule.IDVEHICULES, ct);
    }

    public async Task<object> UpdateVehiculeAsync(long id, UpdateVehiculeDto dto, CancellationToken ct)
    {
        var vehicule = await db.Vehicule.Where(v => v.IDVEHICULES == id).FirstOrThrowAsync(ct);
        Assign(vehicule, dto);
        await db.SaveChangesAsync(ct);
        return await ListViewAsync(id, ct);
    }

    public Task DeleteVehiculeAsync(long id, CancellationToken ct) => db.Vehicule.Where(v => v.IDVEHICULES == id).DeleteOrThrowAsync(ct);

    // Une date vidée ("") est enregistrée NULL. IDSOCIETES "" retire la
    // société (NULL : 0 violerait la clé étrangère).
    private static void Assign(Vehicule v, CreateVehiculeDto dto)
    {
        dto.Type.ApplyInt(x => v.Type = x);
        dto.Marque.Apply(x => v.Marque = x);
        dto.Modele.Apply(x => v.Modele = x);
        dto.Num_police_assurance.Apply(x => v.Num_police_assurance = x);
        dto.Num_immat.Apply(x => v.Num_immat = x);
        dto.Num_chassis.Apply(x => v.Num_chassis = x);
        dto.Date_validite_assurance.ApplyDate(x => v.Date_validite_assurance = x, dateOnly: true);
        dto.Num_licence_transport.Apply(x => v.Num_licence_transport = x);
        dto.Date_validite_licence.ApplyDate(x => v.Date_validite_licence = x, dateOnly: true);
        dto.Date_modification_licence.ApplyDate(x => v.Date_modification_licence = x, dateOnly: true);
        dto.Date_inspection_auto.ApplyDate(x => v.Date_inspection_auto = x, dateOnly: true);
        dto.Date_radiation_immatriculation.ApplyDate(x => v.Date_radiation_immatriculation = x, dateOnly: true);
        dto.Date_vente.ApplyDate(x => v.Date_vente = x, dateOnly: true);
        Dto.OptionalId(dto.IDSOCIETES).Apply(x => v.IDSOCIETES = x);
        dto.Date_premiere_mise_en_circulation.ApplyDate(x => v.Date_premiere_mise_en_circulation = x, dateOnly: true);
        dto.Date_validite_tachygeaphe.ApplyDate(x => v.Date_validite_tachygeaphe = x, dateOnly: true);
        dto.Avec_compresseur.ApplyInt(x => v.Avec_compresseur = x);
    }

    // Forme renvoyée par la création et la modification (celle de la liste).
    private Task<object> ListViewAsync(long id, CancellationToken ct) =>
        db.Vehicule.Where(v => v.IDVEHICULES == id)
            .Select(v => (object)new
            {
                v.IDVEHICULES,
                v.Num_immat,
                v.Type,
                v.Marque,
                v.Modele,
                v.IDSOCIETES,
                Societe = v.Societe == null ? null : new { v.Societe.Nom_societe },
                v.Num_police_assurance,
                v.Num_chassis,
                v.Date_validite_assurance,
                v.Num_licence_transport,
                v.Date_validite_licence,
                v.Date_inspection_auto,
                v.Date_validite_tachygeaphe,
            })
            .FirstOrThrowAsync(ct);
}

// ===== CONTROLLER =====
public class VehiculeController(VehiculeService service) : ApiControllerBase
{
    // NestJS : GET vehicules
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("vehicules")]
    public Task<object> GetVehicules([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetVehiculesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    // NestJS : GET vehicules/:id
    [HttpGet("vehicules/{id}")]
    public Task<object> GetVehicule(string id, CancellationToken ct) => service.GetVehiculeAsync(Params.ParseId(id), ct);

    // NestJS : POST vehicules
    [HttpPost("vehicules")]
    public Task<IActionResult> CreateVehicule([Body] CreateVehiculeDto dto, CancellationToken ct) => Created201(service.CreateVehiculeAsync(dto, ct));

    // NestJS : PATCH vehicules/:id
    [HttpPatch("vehicules/{id}")]
    public Task<object> UpdateVehicule(string id, [Body] UpdateVehiculeDto dto, CancellationToken ct) =>
        service.UpdateVehiculeAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE vehicules/:id
    [HttpDelete("vehicules/{id}")]
    public Task DeleteVehicule(string id, CancellationToken ct) => service.DeleteVehiculeAsync(Params.ParseId(id), ct);
}
