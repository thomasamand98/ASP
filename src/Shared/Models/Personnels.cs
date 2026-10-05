namespace Horizon.Shared.Models;

// Réponses de l'API — module Personnel (GET /personnel et /personnel/:id).
public record Personnel
{
    public string IDPERSONNELS { get; init; } = "";
    public string? Civilite_Personnel { get; init; }
    public string? Prenom_Personnel { get; init; }
    public string? Nom_Personnel { get; init; }
    public string? Telephone_professionnel { get; init; }
    public string? Telephone_fixe { get; init; }
    public string? Date_validite_CAP { get; init; }
    public string? Date_validite_selection_medicale { get; init; }
    public string? Date_validite_carte_chauffeur { get; init; }
    public string? Num_service_social { get; init; }
    public string? E_mail_professionnel { get; init; }
}

public sealed record PersonnelDetail : Personnel
{
    public string? Telephone_portable { get; init; }
    public string? Telephone_autre { get; init; }
    public string? Description_telephone { get; init; }
    public string? E_mail { get; init; }
    public string? Num_registre_national { get; init; }
    public string? Date_naissance { get; init; }
    public string? Lieu_naissance { get; init; }
    public string? Pays_Naissance { get; init; }
    public string? Etat_civil { get; init; }
    public double? Nbr_personne_charge { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
    public string? Nom_Banque { get; init; }
    public string? Qualification { get; init; }
    public double? Routier { get; init; }
    public double? Manutention { get; init; }
    public double? Atelier { get; init; }
    public string? Commentaire_Personnel { get; init; }
    public string? IDADRESSES { get; init; }
    public string? Date_validite_carte_identite { get; init; }
    public string? Date_validite_A1 { get; init; }
    public string? Date_validite_SIPSI { get; init; }
    // Adresse liée par IDADRESSES (table `adresses`).
    public AdresseView? Adresse { get; init; }
}
