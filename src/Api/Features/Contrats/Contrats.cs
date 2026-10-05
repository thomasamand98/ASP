using System.Globalization;
using Api.Common;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Features.DocumentMerge;
using Api.Features.Entites;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.Contrats;

// ===== SERVICE =====
public class ContratService(AppDbContext db, EntiteService entiteService, IOptions<AppOptions> options)
{
    // Projection (voir Common/Projection.cs) : pour chaque table source, les
    // lignes de cette table liées aux ids sélectionnés.
    private static readonly ProjectionMap<Contrat> Projections = new()
    {
        ["societes"] = ids => c => c.IDSOCIETES != null && ids.Contains(c.IDSOCIETES.Value),
        ["commandes"] = ids => c => c.Commandes.Any(x => ids.Contains(x.IDCOMMANDES)),
    };

    // page/pageSize optionnels : omis, toute la table. societeId optionnel :
    // ne renvoie que les contrats de cette société — alimente l'onglet
    // « Contrats / Offres » de la fiche Société (SocieteForm.tsx).
    public async Task<object> GetContratsAsync(int? page, int? pageSize, long? societeId, string? search, Projection? projection, CancellationToken ct)
    {
        IQueryable<Contrat> query = db.Contrat;
        if (societeId is not null) query = query.Where(c => c.IDSOCIETES == societeId);
        query = query
            .WhereSearch(search, p => c =>
                EF.Functions.Like(c.Num_contrat, p) || EF.Functions.Like(c.Description_projet, p)
                || EF.Functions.Like(c.Societe!.Nom_societe, p) || EF.Functions.Like(c.Societe!.TVA, p))
            .WhereProjection(Projections, projection);
        var contrats = await ListSelect(query.OrderBy(c => c.IDCONTRATS).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { contrats, total };
    }

    public async Task<object> GetContratPrestationsAsync(long id, CancellationToken ct)
    {
        var prestations = await db.Prestation.Where(p => p.IDCONTRATS == id).OrderBy(p => p.Ordre).ThenBy(p => p.IDPRESTATIONS)
            .Select(p => new { p.IDPRESTATIONS, p.Description_prestation, p.Description_courte })
            .ToListAsync(ct);
        return new { prestations };
    }

    // Fiche complète : tous les champs scripturables du contrat, plus le type
    // de facture et la marchandise résolus, le client de la société (numéro
    // client affiché dans la fiche) et les listes liées des onglets de la
    // fiche (conditions CMR, prestations, factures).
    // `Chiffre_affaires` (bloc « Chiffres clé ») : somme HT des factures du
    // contrat, hors proformas (pas de vraies factures).
    public async Task<object> GetContratAsync(long id, CancellationToken ct)
    {
        var c = await db.Contrat.AsNoTracking().AsSplitQuery()
            .Include(x => x.Societe).ThenInclude(s => s!.Client)
            .Include(x => x.ConditionCmrs)
            .Include(x => x.Prestations).ThenInclude(p => p.Marchandise)
            .Include(x => x.Factures)
            .Include(x => x.Marchandise)
            .Include(x => x.TypeFacture)
            .Where(x => x.IDCONTRATS == id)
            .FirstOrThrowAsync(ct);
        var montants = await db.Facture
            .Where(f => f.IDCONTRATS == id && (f.Proformat == null || f.Proformat != 1) && f.Montant_Facture_HT != null)
            .Select(f => f.Montant_Facture_HT!.Value)
            .ToListAsync(ct);

        return new
        {
            c.IDCONTRATS,
            c.Num_contrat,
            c.Version_contrat,
            c.Description_projet,
            c.Date_debut,
            c.Date_fin,
            c.IDSOCIETES,
            Societe = c.Societe is null ? null : new
            {
                c.Societe.Nom_societe,
                c.Societe.TVA,
                Client = c.Societe.Client is null ? null : new { c.Societe.Client.Numero_client, c.Societe.Client.Taux_tva },
            },
            ConditionCmrs = c.ConditionCmrs.OrderBy(x => x.IDCONDITIONS_CMR).Select(x => new { x.IDCONDITIONS_CMR, x.Libelle, x.Boite_a_cocher }),
            Prestations = c.Prestations.OrderBy(p => p.Ordre).ThenBy(p => p.IDPRESTATIONS).Select(p => new
            {
                p.IDPRESTATIONS,
                p.Ordre,
                p.Description_prestation,
                p.Prix_unitaire,
                p.Unite,
                Marchandise = p.Marchandise is null ? null : new { p.Marchandise.Nom_marchandise },
            }),
            Factures = c.Factures.OrderByDescending(f => f.Date_Facture).ThenByDescending(f => f.IDFACTURES).Select(f => new
            {
                f.IDFACTURES,
                f.num_Facture,
                f.Date_Facture,
                f.Date_echeance,
                f.Montant_Facture_HT,
                f.Taux_TVA,
                f.etat_Facture,
                f.Proformat,
                f.Note_de_Credit,
            }),
            c.IDTYPES_FACTURE,
            c.Annee_archivage,
            c.Offre_de_prix,
            c.Note_confidentielle,
            c.Instruction_CMR,
            c.Archive,
            c.Reference_client,
            c.Taux_tva,
            c.Qt_client_facturation,
            c.Suivant,
            c.IDMARCHANDISES,
            c.Commissionnaire,
            c.Type_contrat,
            Marchandise = c.Marchandise is null ? null : new { c.Marchandise.Nom_marchandise },
            TypeFacture = c.TypeFacture is null ? null : new { c.TypeFacture.Nom },
            Chiffre_affaires = montants.Count == 0 ? (decimal?)null : montants.Sum(),
        };
    }

    // Contexte de données pour la fusion PDF (voir DocumentMergeService,
    // appelé par ContratController.GetContratPdf) : même forme, mêmes clés
    // racine (Entite/Contrat/Divers) que
    // frontend/src/features/documents/mergeFields.ts, pour que les chemins
    // posés par l'éditeur s'y résolvent directement.
    //
    // Unite (Prestations/PrestationSupplementaires) est un code numérique de
    // la catégorie d'énumération « unite_prestation » — résolu ici en libellé,
    // pour ne pas imprimer un code brut. Type n'a pas d'équivalent connu :
    // imprimé tel quel.
    public async Task<Dictionary<string, object?>> GetContratMergeContextAsync(long id, CancellationToken ct)
    {
        var contrat = await db.Contrat.AsNoTracking().AsSplitQuery()
            .Include(x => x.TypeFacture)
            .Include(x => x.Societe).ThenInclude(s => s!.Client)
            .Include(x => x.Societe).ThenInclude(s => s!.Adresse)
            .Include(x => x.Prestations).ThenInclude(p => p.Marchandise)
            .Include(x => x.Prestations).ThenInclude(p => p.PrestationSupplementaires)
            .Where(x => x.IDCONTRATS == id)
            .FirstOrThrowAsync(ct);
        var entite = await entiteService.GetEntiteAsync(ct);
        var unites = await db.Enumeration.Where(e => e.CategorieEnumeration!.Nom == "unite_prestation")
            .OrderBy(e => e.Ordre).ThenBy(e => e.Valeur_affiche).ThenBy(e => e.IDENUMERATIONS)
            .Select(e => new { e.Valeur, e.Valeur_affiche })
            .ToListAsync(ct);
        var uniteLabels = new Dictionary<string, string?>();
        foreach (var unite in unites.Where(u => u.Valeur is not null)) uniteLabels[unite.Valeur!] = unite.Valeur_affiche;
        string? UniteLabel(int? code)
        {
            if (code is null) return null;
            var key = code.Value.ToString(CultureInfo.InvariantCulture);
            return uniteLabels.GetValueOrDefault(key) ?? key;
        }

        var societe = contrat.Societe;
        return new Dictionary<string, object?>
        {
            ["Entite"] = entite,
            ["Divers"] = new { Date_edition = DateTime.UtcNow, CGV_URL = string.IsNullOrEmpty(options.Value.CgvUrl) ? null : options.Value.CgvUrl },
            ["Contrat"] = new
            {
                contrat.Num_contrat,
                contrat.Version_contrat,
                contrat.Date_debut,
                contrat.Date_fin,
                contrat.Reference_client,
                contrat.Taux_tva,
                contrat.Instruction_CMR,
                contrat.Description_projet,
                contrat.Commissionnaire,
                TypeFacture = contrat.TypeFacture is null ? null : new { contrat.TypeFacture.Nom },
                Societe = societe is null ? null : new
                {
                    societe.Nom_societe,
                    societe.Denomination,
                    societe.TVA,
                    Client = societe.Client is null ? null : new { societe.Client.Numero_client, societe.Client.Delai_paiement },
                    Adresse = societe.Adresse is null ? null : new { societe.Adresse.Adresse1, societe.Adresse.CP, societe.Adresse.Localite, societe.Adresse.Pays_full_name },
                },
                Prestations = contrat.Prestations.OrderBy(p => p.Ordre).ThenBy(p => p.IDPRESTATIONS).Select(p => new
                {
                    p.Type,
                    p.Description_prestation,
                    p.Description_courte,
                    p.Prix_unitaire,
                    Unite = UniteLabel(p.Unite),
                    p.Instruction,
                    Marchandise = p.Marchandise is null ? null : new { p.Marchandise.Nom_marchandise },
                    PrestationSupplementaires = p.PrestationSupplementaires.OrderBy(s => s.IDPRESTATIONS_SUPPLEMENTAIRES)
                        .Select(s => new { s.Description_prestation, s.Prix_unitaire, Unite = UniteLabel(s.Unite) })
                        .ToList(),
                }).ToList(),
            },
        };
    }

    public async Task<object> CreateContratAsync(CreateContratDto dto, CancellationToken ct)
    {
        // IDSOCIETES/IDTYPES_FACTURE/IDMARCHANDISES/IDUTILISATEURS_* ont un
        // défaut DB de 0, qui viole leur contrainte de clé étrangère quand ils
        // sont omis — mis explicitement à NULL.
        var contrat = new Contrat
        {
            IDSOCIETES = null,
            IDTYPES_FACTURE = null,
            IDMARCHANDISES = null,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
        };
        Assign(contrat, dto);
        db.Contrat.Add(contrat);
        await db.SaveChangesAsync(ct);
        return await ListSelect(db.Contrat.Where(c => c.IDCONTRATS == contrat.IDCONTRATS)).FirstOrThrowAsync(ct);
    }

    public async Task<object> UpdateContratAsync(long id, UpdateContratDto dto, CancellationToken ct)
    {
        var contrat = await db.Contrat.Where(c => c.IDCONTRATS == id).FirstOrThrowAsync(ct);
        Assign(contrat, dto);
        await db.SaveChangesAsync(ct);
        return await ListSelect(db.Contrat.Where(c => c.IDCONTRATS == id)).FirstOrThrowAsync(ct);
    }

    public Task DeleteContratAsync(long id, CancellationToken ct) => db.Contrat.Where(c => c.IDCONTRATS == id).DeleteOrThrowAsync(ct);

    // Champ absent = inchangé ; chaîne vide = effacé (NULL), pour que la fiche
    // puisse vider une date de fin, une société, un type de facture...
    private static void Assign(Contrat c, CreateContratDto dto)
    {
        dto.Num_contrat.Apply(v => c.Num_contrat = v);
        dto.Description_projet.Apply(v => c.Description_projet = v);
        dto.Date_debut.ApplyDate(v => c.Date_debut = v, dateOnly: true);
        dto.Date_fin.ApplyDate(v => c.Date_fin = v, dateOnly: true);
        Dto.OptionalId(dto.IDSOCIETES).Apply(v => c.IDSOCIETES = v);
        Dto.OptionalId(dto.IDTYPES_FACTURE).Apply(v => c.IDTYPES_FACTURE = v);
        dto.Annee_archivage.Apply(v => c.Annee_archivage = v);
        dto.Offre_de_prix.ApplyInt(v => c.Offre_de_prix = v);
        dto.Note_confidentielle.Apply(v => c.Note_confidentielle = v);
        dto.Instruction_CMR.Apply(v => c.Instruction_CMR = v);
        dto.Version_contrat.Apply(v => c.Version_contrat = v);
        dto.Archive.ApplyInt(v => c.Archive = v);
        dto.Reference_client.Apply(v => c.Reference_client = v);
        dto.Taux_tva.Apply(v => c.Taux_tva = v.Length == 0 ? null : decimal.Parse(v, CultureInfo.InvariantCulture));
        dto.Qt_client_facturation.ApplyInt(v => c.Qt_client_facturation = v);
        dto.Suivant.ApplyInt(v => c.Suivant = v);
        Dto.OptionalId(dto.IDMARCHANDISES).Apply(v => c.IDMARCHANDISES = v);
        dto.Commissionnaire.Apply(v => c.Commissionnaire = v);
        dto.Type_contrat.Apply(v => c.Type_contrat = v);
    }

    private static IQueryable<object> ListSelect(IQueryable<Contrat> query) => query.Select(c => new
    {
        c.IDCONTRATS,
        c.Num_contrat,
        // Numéro affiché « Num_contrat.Version_contrat » (ex. 1764.24/01).
        c.Version_contrat,
        c.Description_projet,
        c.Date_debut,
        c.Date_fin,
        c.IDSOCIETES,
        Societe = c.Societe == null ? null : new { c.Societe.Nom_societe, c.Societe.TVA },
    });
}

// ===== CONTROLLER =====
public class ContratController(ContratService service, DocumentMergeService documentMerge) : ApiControllerBase
{
    // NestJS : GET contrats
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    // ?societeId=X optionnel : ne renvoie que les contrats de cette société.
    [HttpGet("contrats")]
    public Task<object> GetContrats([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? societeId, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetContratsAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), Params.ParseOptionalId(societeId), search, Projection.Parse(via, ids), ct);

    // NestJS : GET contrats/:id
    [HttpGet("contrats/{id}")]
    public Task<object> GetContrat(string id, CancellationToken ct) => service.GetContratAsync(Params.ParseId(id), ct);

    // NestJS : GET contrats/:id/prestations
    // Prestations du contrat, pour le sélecteur de la fiche commande.
    [HttpGet("contrats/{id}/prestations")]
    public Task<object> GetContratPrestations(string id, CancellationToken ct) => service.GetContratPrestationsAsync(Params.ParseId(id), ct);

    // NestJS : GET contrats/:id/pdf?templateId=
    // PDF du contrat, fusionné avec le modèle choisi.
    [HttpGet("contrats/{id}/pdf")]
    public async Task<IActionResult> GetContratPdf(string id, [FromQuery] string? templateId, CancellationToken ct)
    {
        var contratId = Params.ParseId(id);
        var template = Params.ParseId(templateId);
        var context = await service.GetContratMergeContextAsync(contratId, ct);
        var pdf = await documentMerge.RenderPdfAsync(template, context, ct);
        Response.Headers.ContentDisposition = $"inline; filename=\"contrat-{contratId}.pdf\"";
        return File(pdf, "application/pdf");
    }

    // NestJS : POST contrats
    [HttpPost("contrats")]
    public Task<IActionResult> CreateContrat([Body] CreateContratDto dto, CancellationToken ct) => Created201(service.CreateContratAsync(dto, ct));

    // NestJS : PATCH contrats/:id
    [HttpPatch("contrats/{id}")]
    public Task<object> UpdateContrat(string id, [Body] UpdateContratDto dto, CancellationToken ct) =>
        service.UpdateContratAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE contrats/:id
    [HttpDelete("contrats/{id}")]
    public Task DeleteContrat(string id, CancellationToken ct) => service.DeleteContratAsync(Params.ParseId(id), ct);
}
