namespace Horizon.Shared;

// IDADRESSES géré côté serveur (Common/Adresses.cs)
public class CreateContactDto : IAdresseFields
{
    [Text] public Opt<string> Civilite { get; set; }
    [Text] public Opt<string> Nom_contact { get; set; }
    [Text] public Opt<string> Prenom_contact { get; set; }
    [Text] public Opt<string> Telephone_portable { get; set; }
    [Text] public Opt<string> Telephone_fixe { get; set; }
    [Text] public Opt<string> Telephone_autre { get; set; }
    [Text] public Opt<string> E_mail { get; set; }
    [Text] public Opt<string> Remarque { get; set; }
    [Num] public Opt<double> Personne_physique { get; set; }
    [Num] public Opt<double> Adresse_entreprise { get; set; }
    [Text] public Opt<string> description_telephone { get; set; }
    // société principale (SocieteContacts), vide en modif = retirée
    [Id] public Opt<string> IDSOCIETES { get; set; }
    [Text] public Opt<string> Fonction_contact { get; set; }
    [Text] public Opt<string> Service_bureau { get; set; }
    // création depuis la fiche Point, ignoré en modif
    [Id] public Opt<string> IDPOINTS { get; set; }
    // adresse aplatie, enregistrée à part dans `adresses`
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
}

[Partial]
public class UpdateContactDto : CreateContactDto;
