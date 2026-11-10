namespace Horizon.Shared;

// adresse aplatie ici mais stockée dans `adresses`
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
    // data URI base64, "" = logo supprimé (pas comme les mots de passe)
    [Text] public Opt<string> Logo { get; set; }
}
