using System.Text;
using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Entites;

// ===== SERVICE =====
// Ressource singleton : une seule ligne dans la table `entites`.
public class EntiteService(AppDbContext db)
{
    // Les mots de passe SMTP ne sont jamais renvoyés : seul un indicateur
    // « défini » l'est. Le logo (data URI stockée en binaire) est renvoyé en
    // texte.
    public async Task<object> GetEntiteAsync(CancellationToken ct)
    {
        var entite = await db.Entite.AsNoTracking().OrderBy(e => e.IDSignaletique).FirstOrThrowAsync(ct);
        var adresse = entite.IDADRESSES is null or 0 ? null : await db.Adresse.AsNoTracking().FirstOrDefaultAsync(a => a.IDADRESSES == entite.IDADRESSES, ct);
        return new
        {
            entite.IDSignaletique,
            entite.Nom_societe,
            entite.Nom_court,
            entite.Num_Telephone,
            entite.Email_contact,
            entite.Num_TVA,
            entite.Nom_Banque,
            entite.Iban,
            entite.Bic,
            entite.Signataire,
            entite.numero_ucm,
            entite.Num_licence,
            entite.Valeur_facial_cheque_repas,
            entite.Seveur_SMTP,
            entite.Port_SMTP,
            entite.Utilisateur_SMTP,
            entite.TypeConnexion_SMTP,
            entite.Utilisateur_smtp_planning,
            entite.IDADRESSES,
            MDP_SMTP_defini = !string.IsNullOrEmpty(entite.MDP_SMTP),
            MDP_SMTP_Planning_defini = !string.IsNullOrEmpty(entite.MDP_SMTP_Planning),
            Logo = entite.Logo is null ? null : Encoding.UTF8.GetString(entite.Logo),
            Adresse = AdresseView.From(adresse),
        };
    }

    public async Task<object> UpdateEntiteAsync(UpdateEntiteDto dto, CancellationToken ct)
    {
        var entite = await db.Entite.OrderBy(e => e.IDSignaletique).FirstOrThrowAsync(ct);
        var currentAdresse = entite.IDADRESSES;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var idAdresses = await Adresses.SaveAsync(db, currentAdresse, Adresses.Pick(dto), ct);
            dto.Nom_societe.Apply(v => entite.Nom_societe = v);
            dto.Nom_court.Apply(v => entite.Nom_court = v);
            dto.Num_Telephone.Apply(v => entite.Num_Telephone = v);
            dto.Email_contact.Apply(v => entite.Email_contact = v);
            dto.Num_TVA.Apply(v => entite.Num_TVA = v);
            dto.Nom_Banque.Apply(v => entite.Nom_Banque = v);
            dto.Iban.Apply(v => entite.Iban = v);
            dto.Bic.Apply(v => entite.Bic = v);
            dto.Signataire.Apply(v => entite.Signataire = v);
            dto.numero_ucm.Apply(v => entite.numero_ucm = v);
            dto.Num_licence.Apply(v => entite.Num_licence = v);
            dto.Valeur_facial_cheque_repas.Apply(v => entite.Valeur_facial_cheque_repas = v);
            dto.Seveur_SMTP.Apply(v => entite.Seveur_SMTP = v);
            dto.Port_SMTP.ApplyInt(v => entite.Port_SMTP = v);
            dto.Utilisateur_SMTP.Apply(v => entite.Utilisateur_SMTP = v);
            dto.TypeConnexion_SMTP.ApplyInt(v => entite.TypeConnexion_SMTP = v);
            dto.Utilisateur_smtp_planning.Apply(v => entite.Utilisateur_smtp_planning = v);
            // Mots de passe : un champ vide laisse la valeur en place.
            if (!string.IsNullOrEmpty(dto.MDP_SMTP.OrDefault)) entite.MDP_SMTP = dto.MDP_SMTP.Value;
            if (!string.IsNullOrEmpty(dto.MDP_SMTP_Planning.OrDefault)) entite.MDP_SMTP_Planning = dto.MDP_SMTP_Planning.Value;
            // Logo : chaîne vide = suppression.
            dto.Logo.Apply(v => entite.Logo = v.Length > 0 ? Encoding.UTF8.GetBytes(v) : null);
            if (idAdresses != currentAdresse) entite.IDADRESSES = idAdresses;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await GetEntiteAsync(ct);
    }
}

// ===== CONTROLLER =====
// Ressource singleton (une seule ligne en base) — pas d'id dans les routes.
public class EntiteController(EntiteService service) : ApiControllerBase
{
    // NestJS : GET entite
    [HttpGet("entite")]
    public Task<object> GetEntite(CancellationToken ct) => service.GetEntiteAsync(ct);

    // NestJS : PATCH entite
    [HttpPatch("entite")]
    public Task<object> UpdateEntite([Body] UpdateEntiteDto dto, CancellationToken ct) => service.UpdateEntiteAsync(dto, ct);
}
