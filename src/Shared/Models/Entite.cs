namespace Horizon.Shared.Models;

// une seule ligne, mots de passe SMTP jamais renvoyés
public sealed record EntiteDetail
{
    public string IDSignaletique { get; init; } = "";
    public string? Nom_societe { get; init; }
    public string? Nom_court { get; init; }
    public string? Num_Telephone { get; init; }
    public string? Email_contact { get; init; }
    public string? Num_TVA { get; init; }
    public string? Nom_Banque { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
    public string? Signataire { get; init; }
    public string? numero_ucm { get; init; }
    public string? Num_licence { get; init; }
    public double? Valeur_facial_cheque_repas { get; init; }
    public string? Seveur_SMTP { get; init; }
    public double? Port_SMTP { get; init; }
    public string? Utilisateur_SMTP { get; init; }
    public bool MDP_SMTP_defini { get; init; }
    public double? TypeConnexion_SMTP { get; init; }
    public string? Utilisateur_smtp_planning { get; init; }
    public bool MDP_SMTP_Planning_defini { get; init; }
    public string? IDADRESSES { get; init; }
    public string? Logo { get; init; }
    public AdresseView? Adresse { get; init; }
}
