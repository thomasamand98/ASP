namespace Horizon.Shared.Models;

// Réponses de l'API — module Contrats (GET /contrats, /contrats/:id) et
// conditions CMR (POST/PATCH /conditions-cmr). Les Decimal (prix, montants,
// taux) arrivent en texte.
public sealed record Contrat
{
    public string IDCONTRATS { get; init; } = "";
    public string? Num_contrat { get; init; }
    public string? Version_contrat { get; init; }
    public string? Description_projet { get; init; }
    public string? Date_debut { get; init; }
    public string? Date_fin { get; init; }
    public string? IDSOCIETES { get; init; }
    public ContratSociete? Societe { get; init; }
}

public sealed record ContratSociete(string? Nom_societe, string? TVA);

public sealed record ConditionCmr(string IDCONDITIONS_CMR, string? Libelle, double? Boite_a_cocher);

// Unite : code de l'énumération « unite_prestation ».
public sealed record ContratPrestation(string IDPRESTATIONS, double? Ordre, string? Description_prestation, string? Prix_unitaire, double? Unite, MarchandiseNom? Marchandise);

public sealed record MarchandiseNom(string? Nom_marchandise);

// etat_Facture : code de l'énumération « etat_facture » ; Proformat /
// Note_de_Credit : codes 0/1.
public sealed record ContratFacture(string IDFACTURES, string? num_Facture, string? Date_Facture, string? Date_echeance, string? Montant_Facture_HT,
    double? Taux_TVA, double? etat_Facture, double? Proformat, double? Note_de_Credit);

public sealed record ContratClient(string? Numero_client, string? Taux_tva);

public sealed record ContratDetailSociete(string? Nom_societe, string? TVA, ContratClient? Client);

// Fiche complète : type de facture et marchandise résolus, client de la
// société, listes des onglets et chiffre d'affaires (somme HT des factures
// hors proformas ; null s'il n'y en a aucune).
public sealed record ContratDetail
{
    public string IDCONTRATS { get; init; } = "";
    public string? Num_contrat { get; init; }
    public string? Description_projet { get; init; }
    public string? Date_debut { get; init; }
    public string? Date_fin { get; init; }
    public string? IDSOCIETES { get; init; }
    public ContratDetailSociete? Societe { get; init; }
    public string? IDTYPES_FACTURE { get; init; }
    public string? Annee_archivage { get; init; }
    public double? Offre_de_prix { get; init; }
    public string? Note_confidentielle { get; init; }
    public string? Instruction_CMR { get; init; }
    public string? Version_contrat { get; init; }
    public double? Archive { get; init; }
    public string? Reference_client { get; init; }
    public string? Taux_tva { get; init; }
    public double? Qt_client_facturation { get; init; }
    public double? Suivant { get; init; }
    public string? IDMARCHANDISES { get; init; }
    public string? Commissionnaire { get; init; }
    public string? Type_contrat { get; init; }
    public MarchandiseNom? Marchandise { get; init; }
    public TypeFactureNom? TypeFacture { get; init; }
    public List<ConditionCmr> ConditionCmrs { get; init; } = [];
    public List<ContratPrestation> Prestations { get; init; } = [];
    public List<ContratFacture> Factures { get; init; } = [];
    public string? Chiffre_affaires { get; init; }
}

public sealed record TypeFactureNom(string? Nom);

// GET /types-facture.
public sealed record TypeFacture(string IDTYPES_FACTURE, string? Nom);

public sealed record TypesFactureResponse(List<TypeFacture> typesFacture);

// Réponse de GET /societes/:id utile à la fiche contrat (client de la société).
public sealed record SocieteClient(ContratClient? Client);
