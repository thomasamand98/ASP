using Horizon.Shared.Models;

namespace Horizon.Web.Features.Entite;

// Valeurs du formulaire « Coordonnée entreprise » (même noms que
// UpdateEntiteDto) : tout en string/double, adresse aplatie.
public sealed class EntiteModel
{
    public string Nom_societe { get; set; } = "";
    public string Nom_court { get; set; } = "";
    public string Num_Telephone { get; set; } = "";
    public string Email_contact { get; set; } = "";
    public string Num_TVA { get; set; } = "";
    public string Nom_Banque { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Bic { get; set; } = "";
    public string Signataire { get; set; } = "";
    public string numero_ucm { get; set; } = "";
    public string Num_licence { get; set; } = "";
    public double Valeur_facial_cheque_repas { get; set; }
    public string Seveur_SMTP { get; set; } = "";
    public double Port_SMTP { get; set; }
    public string Utilisateur_SMTP { get; set; } = "";
    // Jamais renvoyés par l'API : vides tant que l'utilisateur n'en saisit pas
    // un nouveau (vide = inchangé côté serveur).
    public string MDP_SMTP { get; set; } = "";
    public double TypeConnexion_SMTP { get; set; }
    public string Utilisateur_smtp_planning { get; set; } = "";
    public string MDP_SMTP_Planning { get; set; } = "";
    public string Adresse1 { get; set; } = "";
    public string Adresse2 { get; set; } = "";
    public string Adresse3 { get; set; } = "";
    public string CP { get; set; } = "";
    public string Localite { get; set; } = "";
    public string Pays { get; set; } = "";
    public string Pays_full_name { get; set; } = "";
    // Chaîne vide = supprime le logo.
    public string Logo { get; set; } = "";

    public static EntiteModel From(EntiteDetail d) => new()
    {
        Nom_societe = d.Nom_societe ?? "",
        Nom_court = d.Nom_court ?? "",
        Num_Telephone = d.Num_Telephone ?? "",
        Email_contact = d.Email_contact ?? "",
        Num_TVA = d.Num_TVA ?? "",
        Nom_Banque = d.Nom_Banque ?? "",
        Iban = d.Iban ?? "",
        Bic = d.Bic ?? "",
        Signataire = d.Signataire ?? "",
        numero_ucm = d.numero_ucm ?? "",
        Num_licence = d.Num_licence ?? "",
        Valeur_facial_cheque_repas = d.Valeur_facial_cheque_repas ?? 0,
        Seveur_SMTP = d.Seveur_SMTP ?? "",
        Port_SMTP = d.Port_SMTP ?? 0,
        Utilisateur_SMTP = d.Utilisateur_SMTP ?? "",
        TypeConnexion_SMTP = d.TypeConnexion_SMTP ?? 0,
        Utilisateur_smtp_planning = d.Utilisateur_smtp_planning ?? "",
        Adresse1 = d.Adresse?.Adresse1 ?? "",
        Adresse2 = d.Adresse?.Adresse2 ?? "",
        Adresse3 = d.Adresse?.Adresse3 ?? "",
        CP = d.Adresse?.CP ?? "",
        Localite = d.Adresse?.Localite ?? "",
        Pays = d.Adresse?.Pays ?? "",
        Pays_full_name = d.Adresse?.Pays_full_name ?? "",
        Logo = d.Logo ?? "",
    };
}
