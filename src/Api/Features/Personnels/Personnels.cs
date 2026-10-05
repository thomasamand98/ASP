using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Personnels;

// ===== SERVICE =====
public class PersonnelService(AppDbContext db)
{
    // Projection (voir Common/Projection.cs) : pour chaque table source, les
    // lignes de cette table liées aux ids sélectionnés.
    private static readonly ProjectionMap<Personnel> Projections = new()
    {
        ["chauffeurs"] = ids => p => p.Chauffeurs.Any(c => ids.Contains(c.IDCHAUFFEURS)),
        ["pointage"] = ids => p => p.Pointages.Any(x => ids.Contains(x.IDPOINTAGES)),
        // Attelages de référence : liés au salarié via sa fiche chauffeur.
        ["attelages"] = ids => p => p.Chauffeurs.Any(c => c.AttelageReferences.Any(a => ids.Contains(a.IDATTELAGE_REFERENCE))),
    };

    // page/pageSize optionnels : omis, toute la table.
    public async Task<object> GetPersonnelsAsync(int? page, int? pageSize, string? search, Projection? projection, CancellationToken ct)
    {
        var query = db.Personnel
            .WhereSearch(search, p => x =>
                EF.Functions.Like(x.Civilite_Personnel, p) || EF.Functions.Like(x.Prenom_Personnel, p) || EF.Functions.Like(x.Nom_Personnel, p)
                || EF.Functions.Like(x.Telephone_professionnel, p) || EF.Functions.Like(x.Telephone_fixe, p) || EF.Functions.Like(x.Num_service_social, p))
            .WhereProjection(Projections, projection);
        var personnel = await ListSelect(query.OrderBy(p => p.IDPERSONNELS).Paginate(page, pageSize)).ToListAsync(ct);
        var total = await query.CountAsync(ct);
        return new { personnel, total };
    }

    // Détail : ajoute les champs scripturables absents de la liste. Pas de
    // relation entre Personnel et Adresse (colonne IDADRESSES sans clé
    // déclarée) : l'adresse est lue à part.
    public async Task<object> GetPersonnelAsync(long id, CancellationToken ct)
    {
        var p = await db.Personnel.AsNoTracking().Where(x => x.IDPERSONNELS == id).FirstOrThrowAsync(ct);
        var adresse = p.IDADRESSES is null or 0 ? null : await db.Adresse.AsNoTracking().FirstOrDefaultAsync(a => a.IDADRESSES == p.IDADRESSES, ct);
        return new
        {
            p.IDPERSONNELS,
            p.Civilite_Personnel,
            p.Prenom_Personnel,
            p.Nom_Personnel,
            p.Telephone_professionnel,
            p.Telephone_fixe,
            p.Date_validite_CAP,
            p.Date_validite_selection_medicale,
            p.Date_validite_carte_chauffeur,
            p.Num_service_social,
            p.E_mail_professionnel,
            p.Telephone_portable,
            p.Telephone_autre,
            p.Description_telephone,
            p.E_mail,
            p.Num_registre_national,
            p.Date_naissance,
            p.Lieu_naissance,
            p.Pays_Naissance,
            p.Etat_civil,
            p.Nbr_personne_charge,
            p.Iban,
            p.Bic,
            p.Nom_Banque,
            p.Qualification,
            p.Routier,
            p.Manutention,
            p.Atelier,
            p.Commentaire_Personnel,
            p.IDADRESSES,
            p.Date_validite_carte_identite,
            p.Date_validite_A1,
            p.Date_validite_SIPSI,
            Adresse = AdresseView.From(adresse),
        };
    }

    // L'adresse éventuellement saisie est créée dans `adresses` puis liée.
    public async Task<object> CreatePersonnelAsync(CreatePersonnelDto dto, CancellationToken ct)
    {
        // IDADRESSES/IDUTILISATEURS_* ont un défaut DB de 0, qui viole leur
        // contrainte de clé étrangère quand ils sont omis — mis explicitement
        // à NULL.
        var personnel = new Personnel { IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        Assign(personnel, dto);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        personnel.IDADRESSES = await Adresses.SaveAsync(db, null, Adresses.Pick(dto), ct);
        db.Personnel.Add(personnel);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Personnel.Where(p => p.IDPERSONNELS == personnel.IDPERSONNELS)).FirstOrThrowAsync(ct);
    }

    // L'adresse liée est mise à jour, ou créée si le personnel n'en avait pas.
    public async Task<object> UpdatePersonnelAsync(long id, UpdatePersonnelDto dto, CancellationToken ct)
    {
        // Les dates sont vérifiées avant toute lecture (400 avant 404).
        var probe = new Personnel();
        Assign(probe, dto);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var personnel = await db.Personnel.Where(p => p.IDPERSONNELS == id).FirstOrThrowAsync(ct);
        var idAdresses = await Adresses.SaveAsync(db, personnel.IDADRESSES, Adresses.Pick(dto), ct);
        Assign(personnel, dto);
        personnel.IDADRESSES = idAdresses;
        personnel.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ListSelect(db.Personnel.Where(p => p.IDPERSONNELS == id)).FirstOrThrowAsync(ct);
    }

    public Task DeletePersonnelAsync(long id, CancellationToken ct) => db.Personnel.Where(p => p.IDPERSONNELS == id).DeleteOrThrowAsync(ct);

    // Sont convertis ici :
    //   - les Date_* : le formulaire envoie un jour « AAAA-MM-JJ » (colonne
    //     DATE → minuit UTC) ; vide = effacé (NULL) ;
    //   - les textes vides : le formulaire envoie "" pour un champ non rempli,
    //     enregistré NULL comme le faisait WinDev (pas de "" parasites).
    private static void Assign(Personnel p, CreatePersonnelDto dto)
    {
        static void Text(Opt<string> value, Action<string?> assign)
        {
            if (value.IsSet) assign(string.IsNullOrWhiteSpace(value.Value) ? null : value.Value);
        }

        static void Day(Opt<string> value, string field, Action<DateTime?> assign)
        {
            if (value.IsSet) assign(Dto.ToDay(value.Value, field));
        }

        Text(dto.Civilite_Personnel, v => p.Civilite_Personnel = v);
        Text(dto.Nom_Personnel, v => p.Nom_Personnel = v);
        Text(dto.Prenom_Personnel, v => p.Prenom_Personnel = v);
        Text(dto.Telephone_portable, v => p.Telephone_portable = v);
        Text(dto.Telephone_fixe, v => p.Telephone_fixe = v);
        Text(dto.Telephone_autre, v => p.Telephone_autre = v);
        Text(dto.Telephone_professionnel, v => p.Telephone_professionnel = v);
        Text(dto.Description_telephone, v => p.Description_telephone = v);
        Text(dto.E_mail, v => p.E_mail = v);
        Text(dto.E_mail_professionnel, v => p.E_mail_professionnel = v);
        Text(dto.Num_service_social, v => p.Num_service_social = v);
        Text(dto.Num_registre_national, v => p.Num_registre_national = v);
        Text(dto.Lieu_naissance, v => p.Lieu_naissance = v);
        Text(dto.Pays_Naissance, v => p.Pays_Naissance = v);
        Text(dto.Etat_civil, v => p.Etat_civil = v);
        dto.Nbr_personne_charge.ApplyInt(v => p.Nbr_personne_charge = v);
        Text(dto.Iban, v => p.Iban = v);
        Text(dto.Bic, v => p.Bic = v);
        Text(dto.Nom_Banque, v => p.Nom_Banque = v);
        Text(dto.Qualification, v => p.Qualification = v);
        dto.Routier.ApplyInt(v => p.Routier = v);
        dto.Manutention.ApplyInt(v => p.Manutention = v);
        dto.Atelier.ApplyInt(v => p.Atelier = v);
        Text(dto.Commentaire_Personnel, v => p.Commentaire_Personnel = v);
        Day(dto.Date_naissance, nameof(dto.Date_naissance), v => p.Date_naissance = v);
        Day(dto.Date_validite_selection_medicale, nameof(dto.Date_validite_selection_medicale), v => p.Date_validite_selection_medicale = v);
        Day(dto.Date_validite_carte_chauffeur, nameof(dto.Date_validite_carte_chauffeur), v => p.Date_validite_carte_chauffeur = v);
        Day(dto.Date_validite_CAP, nameof(dto.Date_validite_CAP), v => p.Date_validite_CAP = v);
        Day(dto.Date_validite_carte_identite, nameof(dto.Date_validite_carte_identite), v => p.Date_validite_carte_identite = v);
        Day(dto.Date_validite_A1, nameof(dto.Date_validite_A1), v => p.Date_validite_A1 = v);
        Day(dto.Date_validite_SIPSI, nameof(dto.Date_validite_SIPSI), v => p.Date_validite_SIPSI = v);
    }

    private static IQueryable<object> ListSelect(IQueryable<Personnel> query) => query.Select(p => new
    {
        p.IDPERSONNELS,
        p.Civilite_Personnel,
        p.Prenom_Personnel,
        p.Nom_Personnel,
        p.Telephone_professionnel,
        p.Telephone_fixe,
        p.Date_validite_CAP,
        p.Date_validite_selection_medicale,
        p.Date_validite_carte_chauffeur,
        p.Num_service_social,
        // Adresse « Email planning » affichée sur la fiche chauffeur liée.
        p.E_mail_professionnel,
    });
}

// ===== CONTROLLER =====
public class PersonnelController(PersonnelService service) : ApiControllerBase
{
    // NestJS : GET personnel
    // ?page=1&pageSize=25 : optionnels — omis, renvoie toute la table.
    [HttpGet("personnel")]
    public Task<object> GetPersonnels([FromQuery] string? page, [FromQuery] string? pageSize, [FromQuery] string? search, [FromQuery] string? via, [FromQuery] string? ids, CancellationToken ct) =>
        service.GetPersonnelsAsync(Params.ParsePage(page), Params.ParsePageSize(pageSize), search, Projection.Parse(via, ids), ct);

    // NestJS : GET personnel/:id
    [HttpGet("personnel/{id}")]
    public Task<object> GetPersonnel(string id, CancellationToken ct) => service.GetPersonnelAsync(Params.ParseId(id), ct);

    // NestJS : POST personnel
    [HttpPost("personnel")]
    public Task<IActionResult> CreatePersonnel([Body] CreatePersonnelDto dto, CancellationToken ct) => Created201(service.CreatePersonnelAsync(dto, ct));

    // NestJS : PATCH personnel/:id
    [HttpPatch("personnel/{id}")]
    public Task<object> UpdatePersonnel(string id, [Body] UpdatePersonnelDto dto, CancellationToken ct) =>
        service.UpdatePersonnelAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE personnel/:id
    [HttpDelete("personnel/{id}")]
    public Task DeletePersonnel(string id, CancellationToken ct) => service.DeletePersonnelAsync(Params.ParseId(id), ct);
}
