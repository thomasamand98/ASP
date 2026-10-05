using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Contacts;

// ===== SERVICE =====
public class ContactService(AppDbContext db)
{
    // Projection (voir Common/Projection.cs) : pour chaque table source, les
    // lignes de cette table liées aux ids sélectionnés.
    private static readonly ProjectionMap<Contact> Projections = new()
    {
        ["societes"] = ids => c => c.SocieteContacts.Any(sc => sc.IDSOCIETES != null && ids.Contains(sc.IDSOCIETES.Value)),
        ["points"] = ids => c => c.PointContacts.Any(pc => pc.IDPOINTS != null && ids.Contains(pc.IDPOINTS.Value)),
    };

    // page/pageSize optionnels : omis, toute la table.
    // societeId optionnel : ne renvoie que les contacts liés à cette société
    // (onglet Contacts de la fiche Société) — les colonnes Fonction/Service
    // affichent alors le lien avec CETTE société plutôt que le premier lien
    // trouvé.
    // pointId optionnel : ne renvoie que les contacts liés à ce point (onglet
    // Contacts de la fiche Point), avec leur lien à ce point (PointContacts :
    // Lien, Recevoir_Mail_Planning).
    public async Task<object> GetContactsAsync(int? page, int? pageSize, string? search, long? societeId, Projection? projection, long? pointId, CancellationToken ct)
    {
        IQueryable<Contact> query = db.Contact;
        if (societeId is not null) query = query.Where(c => c.SocieteContacts.Any(sc => sc.IDSOCIETES == societeId));
        if (pointId is not null) query = query.Where(c => c.PointContacts.Any(pc => pc.IDPOINTS == pointId));
        query = query
            .WhereSearch(search, p => c =>
                EF.Functions.Like(c.Civilite, p) || EF.Functions.Like(c.Nom_contact, p) || EF.Functions.Like(c.Prenom_contact, p)
                || EF.Functions.Like(c.Telephone_portable, p) || EF.Functions.Like(c.Telephone_fixe, p) || EF.Functions.Like(c.E_mail, p)
                || c.SocieteContacts.Any(sc => EF.Functions.Like(sc.Fonction_contact, p))
                || c.SocieteContacts.Any(sc => EF.Functions.Like(sc.Service_bureau, p))
                || c.SocieteContacts.Any(sc => EF.Functions.Like(sc.Societe!.Nom_societe, p)))
            .WhereProjection(Projections, projection);

        var ids = await query.OrderBy(c => c.IDCONTACTS).Paginate(page, pageSize).Select(c => c.IDCONTACTS).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { contacts = await ListViewAsync(ids, societeId, pointId, ct), total };
    }

    // Fiche complète : tous les champs scripturables du contact, plus la
    // totalité de ses rattachements société/point (contrairement à la liste,
    // qui n'en garde qu'un seul). Le premier lien société est la « société
    // principale » modifiable dans la fiche.
    public async Task<object> GetContactAsync(long id, CancellationToken ct)
    {
        var contact = await db.Contact.AsNoTracking().Include(c => c.Adresse).Where(c => c.IDCONTACTS == id).FirstOrThrowAsync(ct);
        var societeContacts = await db.SocieteContact.Where(sc => sc.IDCONTACTS == id).OrderBy(sc => sc.IDSOCIETES_CONTACTS)
            .Select(sc => new
            {
                sc.IDSOCIETES,
                sc.Type_lien,
                sc.Fonction_contact,
                sc.Service_bureau,
                Societe = sc.Societe == null ? null : new { sc.Societe.Nom_societe },
            })
            .ToListAsync(ct);
        var pointContacts = await db.PointContact.Where(pc => pc.IDCONTACTS == id).OrderBy(pc => pc.IDPOINT_CONTACTS)
            .Select(pc => new { pc.Lien, pc.Recevoir_Mail_Planning, Point = pc.Point == null ? null : new { pc.Point.Libelle } })
            .ToListAsync(ct);
        return new
        {
            contact.IDCONTACTS,
            contact.Civilite,
            contact.Nom_contact,
            contact.Prenom_contact,
            contact.Telephone_portable,
            contact.Telephone_fixe,
            contact.E_mail,
            SocieteContacts = societeContacts,
            contact.Telephone_autre,
            contact.Remarque,
            contact.Personne_physique,
            contact.Adresse_entreprise,
            contact.description_telephone,
            contact.IDADRESSES,
            Adresse = AdresseView.From(contact.Adresse),
            PointContacts = pointContacts,
        };
    }

    // dto.IDSOCIETES (création depuis la fiche Société) : le contact est créé
    // directement lié à cette société ; de même pour dto.IDPOINTS (création
    // depuis la fiche Point). L'adresse éventuellement saisie est créée dans
    // `adresses` puis liée.
    public async Task<object> CreateContactAsync(CreateContactDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, null, Adresses.Pick(dto), ct);
        // IDADRESSES/IDUTILISATEURS_* ont un défaut DB de 0, qui viole leur
        // contrainte de clé étrangère quand ils sont omis — mis explicitement
        // à NULL.
        var contact = new Contact { IDADRESSES = idAdresses, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        Assign(contact, dto);
        if (!string.IsNullOrEmpty(dto.IDSOCIETES.OrDefault))
        {
            contact.SocieteContacts.Add(new SocieteContact
            {
                IDSOCIETES = Dto.Id(dto.IDSOCIETES.Value),
                Fonction_contact = NullIfEmpty(dto.Fonction_contact),
                Service_bureau = NullIfEmpty(dto.Service_bureau),
                IDUTILISATEURS_createur = null,
                IDUTILISATEURS_modificateur = null,
            });
        }
        if (!string.IsNullOrEmpty(dto.IDPOINTS.OrDefault))
        {
            // IDSOCIETES a un défaut DB de 0 (clé étrangère) : NULL.
            contact.PointContacts.Add(new PointContact { IDPOINTS = Dto.Id(dto.IDPOINTS.Value), IDSOCIETES = null });
        }
        db.Contact.Add(contact);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ListViewAsync([contact.IDCONTACTS], null, null, ct))[0];
    }

    // dto.IDSOCIETES présent : met à jour la « société principale » du
    // contact (son plus ancien lien SocieteContact) — créée si le contact n'en
    // a pas, supprimée si la valeur est vide. Les autres liens sont inchangés.
    // L'adresse liée est mise à jour, ou créée si le contact n'en avait pas.
    public async Task<object> UpdateContactAsync(long id, UpdateContactDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var contact = await db.Contact.Where(c => c.IDCONTACTS == id).FirstOrThrowAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, contact.IDADRESSES, Adresses.Pick(dto), ct);
        if (dto.IDSOCIETES.IsSet)
        {
            var principal = await db.SocieteContact.Where(sc => sc.IDCONTACTS == id).OrderBy(sc => sc.IDSOCIETES_CONTACTS).FirstOrDefaultAsync(ct);
            var societeId = Dto.IdOrNull(dto.IDSOCIETES);
            if (societeId is null)
            {
                if (principal is not null) db.SocieteContact.Remove(principal);
            }
            else
            {
                if (principal is null)
                {
                    principal = new SocieteContact { IDCONTACTS = id, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
                    db.SocieteContact.Add(principal);
                }
                principal.IDSOCIETES = societeId;
                principal.Fonction_contact = NullIfEmpty(dto.Fonction_contact);
                principal.Service_bureau = NullIfEmpty(dto.Service_bureau);
            }
            await db.SaveChangesAsync(ct);
        }
        Assign(contact, dto);
        contact.IDADRESSES = idAdresses;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ListViewAsync([id], null, null, ct))[0];
    }

    public Task DeleteContactAsync(long id, CancellationToken ct) => db.Contact.Where(c => c.IDCONTACTS == id).DeleteOrThrowAsync(ct);

    private static string? NullIfEmpty(Opt<string> value) => string.IsNullOrEmpty(value.OrDefault) ? null : value.Value;

    // Champs du contact lui-même. IDSOCIETES/Fonction_contact/Service_bureau
    // appartiennent au lien SocieteContact, IDPOINTS au lien PointContact ; les
    // champs d'adresse vont dans la table `adresses`.
    private static void Assign(Contact c, CreateContactDto dto)
    {
        dto.Civilite.Apply(v => c.Civilite = v);
        dto.Nom_contact.Apply(v => c.Nom_contact = v);
        dto.Prenom_contact.Apply(v => c.Prenom_contact = v);
        dto.Telephone_portable.Apply(v => c.Telephone_portable = v);
        dto.Telephone_fixe.Apply(v => c.Telephone_fixe = v);
        dto.Telephone_autre.Apply(v => c.Telephone_autre = v);
        dto.E_mail.Apply(v => c.E_mail = v);
        dto.Remarque.Apply(v => c.Remarque = v);
        dto.Personne_physique.ApplyInt(v => c.Personne_physique = v);
        dto.Adresse_entreprise.ApplyInt(v => c.Adresse_entreprise = v);
        dto.description_telephone.Apply(v => c.description_telephone = v);
    }

    // Forme « liste » des contacts demandés, dans l'ordre des ids. Un contact
    // peut être lié à plusieurs sociétés (table de jointure SocieteContacts) —
    // on ne garde que la première (le plus ancien lien, le même que la
    // « société principale » de la fiche), éventuellement restreinte à la
    // société demandée ; idem pour le lien au point demandé.
    private async Task<List<object>> ListViewAsync(List<long> ids, long? societeId, long? pointId, CancellationToken ct)
    {
        var keys = ids.Cast<long?>().ToList();
        var contacts = await db.Contact.Where(c => ids.Contains(c.IDCONTACTS))
            .Select(c => new { c.IDCONTACTS, c.Civilite, c.Nom_contact, c.Prenom_contact, c.Telephone_portable, c.Telephone_fixe, c.E_mail })
            .ToDictionaryAsync(c => c.IDCONTACTS, ct);

        var societeLinks = db.SocieteContact.Where(sc => keys.Contains(sc.IDCONTACTS));
        if (societeId is not null) societeLinks = societeLinks.Where(sc => sc.IDSOCIETES == societeId);
        var firstSociete = (await societeLinks.OrderBy(sc => sc.IDSOCIETES_CONTACTS)
                .Select(sc => new { sc.IDCONTACTS, sc.Fonction_contact, sc.Service_bureau, HasSociete = sc.Societe != null, sc.Societe!.Nom_societe })
                .ToListAsync(ct))
            .GroupBy(sc => sc.IDCONTACTS!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var firstPoint = pointId is null
            ? null
            : (await db.PointContact.Where(pc => keys.Contains(pc.IDCONTACTS) && pc.IDPOINTS == pointId).OrderBy(pc => pc.IDPOINT_CONTACTS)
                    .Select(pc => new { pc.IDCONTACTS, pc.Lien, pc.Recevoir_Mail_Planning })
                    .ToListAsync(ct))
                .GroupBy(pc => pc.IDCONTACTS!.Value)
                .ToDictionary(g => g.Key, g => g.First());

        return ids.Where(contacts.ContainsKey).Select(id =>
        {
            var c = contacts[id];
            var view = new Dictionary<string, object?>
            {
                ["IDCONTACTS"] = c.IDCONTACTS,
                ["Civilite"] = c.Civilite,
                ["Nom_contact"] = c.Nom_contact,
                ["Prenom_contact"] = c.Prenom_contact,
                ["Telephone_portable"] = c.Telephone_portable,
                ["Telephone_fixe"] = c.Telephone_fixe,
                ["E_mail"] = c.E_mail,
                ["SocieteContacts"] = firstSociete.TryGetValue(id, out var sc)
                    ? new object[] { new { sc.Fonction_contact, sc.Service_bureau, Societe = sc.HasSociete ? new { sc.Nom_societe } : null } }
                    : [],
            };
            if (firstPoint is not null)
            {
                view["PointContacts"] = firstPoint.TryGetValue(id, out var pc) ? new object[] { new { pc.Lien, pc.Recevoir_Mail_Planning } } : [];
            }
            return (object)view;
        }).ToList();
    }
}

// ===== CONTROLLER =====
public class ContactController(ContactService service) : ApiControllerBase
{
    // NestJS : GET contacts
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("contacts")]
    public Task<object> GetContacts([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? societeId, [FromQuery] string? via, [FromQuery] string? ids, [FromQuery] string? pointId, CancellationToken ct) =>
        service.GetContactsAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Params.ParseOptionalId(societeId), Projection.Parse(via, ids), Params.ParseOptionalId(pointId), ct);

    // NestJS : GET contacts/:id
    [HttpGet("contacts/{id}")]
    public Task<object> GetContact(string id, CancellationToken ct) => service.GetContactAsync(Params.ParseId(id), ct);

    // NestJS : POST contacts
    [HttpPost("contacts")]
    public Task<IActionResult> CreateContact([Body] CreateContactDto dto, CancellationToken ct) => Created201(service.CreateContactAsync(dto, ct));

    // NestJS : PATCH contacts/:id
    [HttpPatch("contacts/{id}")]
    public Task<object> UpdateContact(string id, [Body] UpdateContactDto dto, CancellationToken ct) =>
        service.UpdateContactAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE contacts/:id
    [HttpDelete("contacts/{id}")]
    public Task DeleteContact(string id, CancellationToken ct) => service.DeleteContactAsync(Params.ParseId(id), ct);
}
