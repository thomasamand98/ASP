namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un contact.
// IDADRESSES n'en fait pas partie : il est géré par le serveur (voir
// Common/Adresses.cs). Personne_physique/Adresse_entreprise sont des codes
// 0/1 (pas des booléens).
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
    // Société principale du contact (liste « Société » de la fiche) : lien
    // SocieteContacts avec sa fonction/service, géré à part du contact
    // lui-même. Vide en modification : retire la société principale.
    [Id] public Opt<string> IDSOCIETES { get; set; }
    [Text] public Opt<string> Fonction_contact { get; set; }
    [Text] public Opt<string> Service_bureau { get; set; }
    // Création depuis la fiche Point : le contact est créé lié à ce point
    // (PointContacts). Ignoré en modification.
    [Id] public Opt<string> IDPOINTS { get; set; }
    // Adresse propre du contact (table `adresses`, liée par IDADRESSES) :
    // champs aplatis, enregistrés à part par le service.
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
