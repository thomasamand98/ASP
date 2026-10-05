namespace Horizon.Shared;

// Champs scripturables de l'entité (coordonnées de la société courante,
// paramètres bancaires et SMTP), plus les champs de l'adresse liée, aplatis
// pour le formulaire mais persistés dans la table `adresses` séparée.
public class UpdateEntiteDto : IAdresseFields
{
    [Text] public Opt<string> Nom_societe { get; set; }
    [Text] public Opt<string> Nom_court { get; set; }
    [Text] public Opt<string> Num_Telephone { get; set; }
    [Text] public Opt<string> Email_contact { get; set; }
    [Text] public Opt<string> Num_TVA { get; set; }
    [Text] public Opt<string> Nom_Banque { get; set; }
    [Text] public Opt<string> Iban { get; set; }
    [Text] public Opt<string> Bic { get; set; }
    [Text] public Opt<string> Signataire { get; set; }
    [Text] public Opt<string> numero_ucm { get; set; }
    [Text] public Opt<string> Num_licence { get; set; }
    [Num] public Opt<double> Valeur_facial_cheque_repas { get; set; }
    [Text] public Opt<string> Seveur_SMTP { get; set; }
    [Num] public Opt<double> Port_SMTP { get; set; }
    [Text] public Opt<string> Utilisateur_SMTP { get; set; }
    [Text] public Opt<string> MDP_SMTP { get; set; }
    [Num] public Opt<double> TypeConnexion_SMTP { get; set; }
    [Text] public Opt<string> Utilisateur_smtp_planning { get; set; }
    [Text] public Opt<string> MDP_SMTP_Planning { get; set; }
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
    // Logo (voir EntiteForm.tsx, glisser-déposer) : data URI base64
    // ("data:image/png;base64,...") envoyée telle quelle par le navigateur.
    // Chaîne vide = supprime le logo (contrairement aux champs mot de passe,
    // qui eux restent inchangés si vides).
    [Text] public Opt<string> Logo { get; set; }
}
