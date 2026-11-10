namespace Horizon.Shared.Models;

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
    public AdresseView? Adresse { get; init; }
}

// taux du plus récent au plus ancien, en texte ("13.400000")
public sealed record TauxHoraire(string IDTAUX_HORAIRE, string? Date_application, string? Taux_horaire, string? Taux_horaire_supplementaire,
    string? Taux_horaire_nuitee, string? Taux_nuitee, string? Taux_samedi, string? Taux_cheque_repas, string Taux_stanby);

public sealed record ContratTravailSociete(string IDSOCIETES, string? Nom_societe);

public sealed record ContratTravail
{
    public string IDCONTRATS_TRAVAIL { get; init; } = "";
    public string? TypeContrat { get; init; }
    public string? Qualification { get; init; }
    public string? Date_debut { get; init; }
    public string? Date_fin { get; init; }
    public double? Routier { get; init; }
    public double? Manutention { get; init; }
    public double? Atelier { get; init; }
    public ContratTravailSociete? Societe { get; init; }
    public List<TauxHoraire> TauxHoraireContratTravails { get; init; } = [];
}

public sealed record ContratsTravailResponse(List<ContratTravail> contrats);

public sealed record PersonnelListResponse(List<Personnel> personnel);
