using System.Globalization;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Societes;

public class SocieteService(AppDbContext db)
{
    private static readonly ProjectionMap<Societe> Projections = new()
    {
        ["contrats"] = ids => s => s.Contrats.Any(c => ids.Contains(c.IDCONTRATS)),
        ["contacts"] = ids => s => s.SocieteContacts.Any(c => c.IDCONTACTS != null && ids.Contains(c.IDCONTACTS.Value)),
        ["chauffeurs"] = ids => s => s.Chauffeurs.Any(c => ids.Contains(c.IDCHAUFFEURS)),
        ["vehicules"] = ids => s => s.Vehicules.Any(v => ids.Contains(v.IDVEHICULES)),
        ["points"] = ids => s => s.Points.Any(p => ids.Contains(p.IDPOINTS)),
    };

    public async Task<object> GetSocietesAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Societe
            .WhereSearch(search, p => s =>
                EF.Functions.Like(s.Nom_societe, p) || EF.Functions.Like(s.Denomination, p) || EF.Functions.Like(s.TVA, p)
                || EF.Functions.Like(s.Activite, p) || EF.Functions.Like(s.Site_web, p))
            .WhereProjection(Projections, projection);
        var societes = await ListSelect(query.OrderBy(s => s.Nom_societe).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { societes, total };
    }

    public async Task<object> GetSocieteAsync(long id, CancellationToken ct)
    {
        var societe = await db.Societe.Where(s => s.IDSOCIETES == id)
            .Select(s => new { s.IDSOCIETES, s.Nom_societe, s.Denomination, s.TVA, s.Activite, s.Site_web, s.Note, s.Prospect, s.Archive, s.Adresse, s.Client, s.Fournisseur })
            .FirstOrThrowAsync(ct);
        var contacts = await db.SocieteContact.Where(sc => sc.IDSOCIETES == id).OrderBy(sc => sc.IDSOCIETES_CONTACTS)
            .Select(sc => new
            {
                sc.Type_lien,
                sc.Fonction_contact,
                sc.Service_bureau,
                Contact = sc.Contact == null ? null : new { sc.Contact.Nom_contact, sc.Contact.Prenom_contact },
            })
            .ToListAsync(ct);
        var clientAdresse = await FindAdresseAsync(societe.Client?.IDADRESSES_facturation, ct);
        var fournisseurAdresse = await FindAdresseAsync(societe.Fournisseur?.IDADRESSES_facturation, ct);
        var (client, fournisseur) = (societe.Client, societe.Fournisseur);

        return new
        {
            societe.IDSOCIETES,
            societe.Nom_societe,
            societe.Denomination,
            societe.TVA,
            societe.Activite,
            societe.Site_web,
            societe.Note,
            societe.Prospect,
            societe.Archive,
            Adresse = AdresseView.From(societe.Adresse),
            SocieteContacts = contacts,
            Client = client is null ? null : new
            {
                client.IDCLIENTS,
                client.Numero_client,
                client.Delai_paiement,
                client.Taux_tva,
                client.E_mail_comptabilite,
                client.Facture_mail,
                client.Adresse_facturation_societe,
                client.IDCONTACTS_Comptabilite,
                Adresse_facturation = clientAdresse,
            },
            Fournisseur = fournisseur is null ? null : new
            {
                fournisseur.IDFOURNISSEURS,
                fournisseur.Numero_fournisseur,
                fournisseur.Delai_paiement,
                fournisseur.Taux_tva,
                fournisseur.E_mail_comptabilite,
                fournisseur.Facture_mail,
                fournisseur.Adresse_facturation_societe,
                fournisseur.IDCONTACTS_Comptabilite,
                fournisseur.Iban,
                fournisseur.Bic,
                Adresse_facturation = fournisseurAdresse,
            },
        };
    }

    public async Task<object> CreateSocieteAsync(CreateSocieteDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, null, Adresses.Pick(dto), ct);
        var clientId = await UpsertClientAsync(null, dto, ct);
        var fournisseurId = await UpsertFournisseurAsync(null, dto, ct);
        var societe = new Societe
        {
            IDADRESSES = idAdresses,
            IDCLIENTS = clientId,
            IDFOURNISSEURS = fournisseurId,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
        };
        Assign(societe, dto);
        db.Societe.Add(societe);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Societe.Where(s => s.IDSOCIETES == societe.IDSOCIETES)).FirstOrThrowAsync(ct);
    }

    public async Task<object> UpdateSocieteAsync(long id, UpdateSocieteDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var societe = await db.Societe.Include(s => s.Client).Include(s => s.Fournisseur).Where(s => s.IDSOCIETES == id).FirstOrThrowAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, societe.IDADRESSES, Adresses.Pick(dto), ct);
        var clientId = await UpsertClientAsync(societe.Client, dto, ct);
        var fournisseurId = await UpsertFournisseurAsync(societe.Fournisseur, dto, ct);
        Assign(societe, dto);
        societe.IDADRESSES = idAdresses;
        societe.IDCLIENTS = clientId;
        societe.IDFOURNISSEURS = fournisseurId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Societe.Where(s => s.IDSOCIETES == id)).FirstOrThrowAsync(ct);
    }

    public Task DeleteSocieteAsync(long id, CancellationToken ct) => db.Societe.Where(s => s.IDSOCIETES == id).DeleteOrThrowAsync(ct);

    private async Task<AdresseView?> FindAdresseAsync(long? id, CancellationToken ct) =>
        id is null or 0 ? null : AdresseView.From(await db.Adresse.FirstOrDefaultAsync(a => a.IDADRESSES == id, ct));

    private static IQueryable<object> ListSelect(IQueryable<Societe> query) =>
        query.Select(s => new { s.IDSOCIETES, s.Nom_societe, s.Denomination, s.TVA, s.Activite, s.Site_web });

    private static void Assign(Societe s, CreateSocieteDto dto)
    {
        dto.Nom_societe.Apply(v => s.Nom_societe = v);
        dto.Denomination.Apply(v => s.Denomination = v);
        dto.TVA.Apply(v => s.TVA = v);
        dto.Activite.Apply(v => s.Activite = v);
        dto.Site_web.Apply(v => s.Site_web = v);
        dto.Note.Apply(v => s.Note = v);
        dto.Prospect.ApplyInt(v => s.Prospect = v);
        dto.Archive.ApplyInt(v => s.Archive = v);
    }

    // créé seulement si numéro de client fourni, null = pas de client
    private async Task<long?> UpsertClientAsync(Client? current, CreateSocieteDto dto, CancellationToken ct)
    {
        if (current is null && string.IsNullOrEmpty(dto.Client_Numero_client.OrDefault)) return null;
        var idAdresses = await Adresses.SaveAsync(db, current?.IDADRESSES_facturation, Adresses.Pick(dto.Client_Adresse_facturation.OrDefault), ct);
        var client = current ?? new Client { IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        dto.Client_Numero_client.Apply(v => client.Numero_client = v);
        dto.Client_Delai_paiement.Apply(v => client.Delai_paiement = v);
        // vide ou absent = inchangé
        if (dto.Client_Taux_tva is { IsSet: true, Value.Length: > 0 }) client.Taux_tva = decimal.Parse(dto.Client_Taux_tva.Value, CultureInfo.InvariantCulture);
        dto.Client_E_mail_comptabilite.Apply(v => client.E_mail_comptabilite = v);
        dto.Client_Facture_mail.ApplyInt(v => client.Facture_mail = v);
        dto.Client_Adresse_facturation_societe.ApplyInt(v => client.Adresse_facturation_societe = v);
        // pas envoyé ou vide = NULL, contrairement aux autres champs
        client.IDCONTACTS_Comptabilite = Dto.IdOrNull(dto.Client_IDCONTACTS_Comptabilite);
        client.IDADRESSES_facturation = idAdresses;
        if (current is null) db.Client.Add(client);
        await db.SaveChangesAsync(ct);
        return client.IDCLIENTS;
    }

    private async Task<long?> UpsertFournisseurAsync(Fournisseur? current, CreateSocieteDto dto, CancellationToken ct)
    {
        if (current is null && string.IsNullOrEmpty(dto.Fournisseur_Numero_fournisseur.OrDefault)) return null;
        var idAdresses = await Adresses.SaveAsync(db, current?.IDADRESSES_facturation, Adresses.Pick(dto.Fournisseur_Adresse_facturation.OrDefault), ct);
        var fournisseur = current ?? new Fournisseur { IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        dto.Fournisseur_Numero_fournisseur.Apply(v => fournisseur.Numero_fournisseur = v);
        dto.Fournisseur_Delai_paiement.Apply(v => fournisseur.Delai_paiement = v);
        if (dto.Fournisseur_Taux_tva is { IsSet: true, Value.Length: > 0 }) fournisseur.Taux_tva = decimal.Parse(dto.Fournisseur_Taux_tva.Value, CultureInfo.InvariantCulture);
        dto.Fournisseur_E_mail_comptabilite.Apply(v => fournisseur.E_mail_comptabilite = v);
        dto.Fournisseur_Facture_mail.ApplyInt(v => fournisseur.Facture_mail = v);
        dto.Fournisseur_Adresse_facturation_societe.ApplyInt(v => fournisseur.Adresse_facturation_societe = v);
        fournisseur.IDCONTACTS_Comptabilite = Dto.IdOrNull(dto.Fournisseur_IDCONTACTS_Comptabilite);
        dto.Fournisseur_Iban.Apply(v => fournisseur.Iban = v);
        dto.Fournisseur_Bic.Apply(v => fournisseur.Bic = v);
        fournisseur.IDADRESSES_facturation = idAdresses;
        if (current is null) db.Fournisseur.Add(fournisseur);
        await db.SaveChangesAsync(ct);
        return fournisseur.IDFOURNISSEURS;
    }
}

public class SocieteController(SocieteService service) : ApiControllerBase
{
    [HttpGet("societes")]
    public Task<object> GetSocietes([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetSocietesAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    [HttpGet("societes/{id}")]
    public Task<object> GetSociete(string id, CancellationToken ct) => service.GetSocieteAsync(Params.ParseId(id), ct);

    [HttpPost("societes")]
    public Task<IActionResult> CreateSociete([Body] CreateSocieteDto dto, CancellationToken ct) => Created201(service.CreateSocieteAsync(dto, ct));

    [HttpPatch("societes/{id}")]
    public Task<object> UpdateSociete(string id, [Body] UpdateSocieteDto dto, CancellationToken ct) =>
        service.UpdateSocieteAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("societes/{id}")]
    public Task DeleteSociete(string id, CancellationToken ct) => service.DeleteSocieteAsync(Params.ParseId(id), ct);
}
