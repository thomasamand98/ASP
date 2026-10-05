namespace Horizon.Shared.Models;

// Réponses de l'API — module Sociétés (GET /societes et /societes/:id).
public record Societe
{
    public string IDSOCIETES { get; init; } = "";
    public string? Nom_societe { get; init; }
    public string? Denomination { get; init; }
    public string? TVA { get; init; }
    public string? Activite { get; init; }
    public string? Site_web { get; init; }
}

// Données de facturation d'un client, résolues dans la fiche Société
// (adresse de facturation résolue, sans son id).
public sealed record ClientBilling
{
    public string IDCLIENTS { get; init; } = "";
    public string? Numero_client { get; init; }
    public string? Delai_paiement { get; init; }
    public string? Taux_tva { get; init; }
    public string? E_mail_comptabilite { get; init; }
    public double? Facture_mail { get; init; }
    public double? Adresse_facturation_societe { get; init; }
    public AdresseView? Adresse_facturation { get; init; }
    public string? IDCONTACTS_Comptabilite { get; init; }
}

public sealed record FournisseurBilling
{
    public string IDFOURNISSEURS { get; init; } = "";
    public string? Numero_fournisseur { get; init; }
    public string? Delai_paiement { get; init; }
    public string? Taux_tva { get; init; }
    public string? E_mail_comptabilite { get; init; }
    public double? Facture_mail { get; init; }
    public double? Adresse_facturation_societe { get; init; }
    public AdresseView? Adresse_facturation { get; init; }
    public string? IDCONTACTS_Comptabilite { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
}

public sealed record SocieteContactLien(string? Type_lien, string? Fonction_contact, string? Service_bureau, SocieteContactNom? Contact);

public sealed record SocieteContactNom(string? Nom_contact, string? Prenom_contact);

public sealed record SocieteDetail : Societe
{
    public string? Note { get; init; }
    public double? Prospect { get; init; }
    public double? Archive { get; init; }
    public AdresseView? Adresse { get; init; }
    public ClientBilling? Client { get; init; }
    public FournisseurBilling? Fournisseur { get; init; }
    public List<SocieteContactLien> SocieteContacts { get; init; } = [];
}
