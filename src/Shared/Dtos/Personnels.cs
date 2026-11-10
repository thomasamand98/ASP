namespace Horizon.Shared;

// Routier/Manutention/Atelier en 0/1
public class CreatePersonnelDto : IAdresseFields
{
    [Text] public Opt<string> Civilite_Personnel { get; set; }
    [Text] public Opt<string> Nom_Personnel { get; set; }
    [Text] public Opt<string> Prenom_Personnel { get; set; }
    [Text] public Opt<string> Telephone_portable { get; set; }
    [Text] public Opt<string> Telephone_fixe { get; set; }
    [Text] public Opt<string> Telephone_autre { get; set; }
    [Text] public Opt<string> Telephone_professionnel { get; set; }
    [Text] public Opt<string> Description_telephone { get; set; }
    [Text] public Opt<string> E_mail { get; set; }
    [Text] public Opt<string> E_mail_professionnel { get; set; }
    [Text] public Opt<string> Num_service_social { get; set; }
    [Text] public Opt<string> Num_registre_national { get; set; }
    [Date] public Opt<string> Date_naissance { get; set; }
    [Text] public Opt<string> Lieu_naissance { get; set; }
    [Text] public Opt<string> Pays_Naissance { get; set; }
    [Text] public Opt<string> Etat_civil { get; set; }
    [Num] public Opt<double> Nbr_personne_charge { get; set; }
    [Text] public Opt<string> Iban { get; set; }
    [Text] public Opt<string> Bic { get; set; }
    [Text] public Opt<string> Nom_Banque { get; set; }
    [Text] public Opt<string> Qualification { get; set; }
    [Num] public Opt<double> Routier { get; set; }
    [Num] public Opt<double> Manutention { get; set; }
    [Num] public Opt<double> Atelier { get; set; }
    [Text] public Opt<string> Commentaire_Personnel { get; set; }
    [Date] public Opt<string> Date_validite_selection_medicale { get; set; }
    [Date] public Opt<string> Date_validite_carte_chauffeur { get; set; }
    [Date] public Opt<string> Date_validite_CAP { get; set; }
    [Date] public Opt<string> Date_validite_carte_identite { get; set; }
    [Date] public Opt<string> Date_validite_A1 { get; set; }
    [Date] public Opt<string> Date_validite_SIPSI { get; set; }
    // adresse aplatie, enregistrée à part
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
}

[Partial]
public class UpdatePersonnelDto : CreatePersonnelDto;
