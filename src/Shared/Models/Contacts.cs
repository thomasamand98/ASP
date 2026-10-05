namespace Horizon.Shared.Models;

// Réponses de l'API — module Contacts (GET /contacts et /contacts/:id).
// SocieteContacts ne contient qu'un élément au plus dans la liste ;
// PointContacts n'est présent qu'avec le filtre pointId (onglet Contacts de
// la fiche Point) : le lien du contact avec CE point.
public sealed record Contact
{
    public string IDCONTACTS { get; init; } = "";
    public string? Civilite { get; init; }
    public string? Nom_contact { get; init; }
    public string? Prenom_contact { get; init; }
    public string? Telephone_portable { get; init; }
    public string? Telephone_fixe { get; init; }
    public string? E_mail { get; init; }
    public List<ContactSociete> SocieteContacts { get; init; } = [];
    public List<ContactPointLien>? PointContacts { get; init; }
}

public sealed record ContactSociete(string? Fonction_contact, string? Service_bureau, NomSociete? Societe);

public sealed record ContactPointLien(string? Lien, double? Recevoir_Mail_Planning);

// Fiche complète : tous les rattachements société/point, du plus ancien au
// plus récent (le premier est la société principale modifiable).
public sealed record ContactDetail
{
    public string IDCONTACTS { get; init; } = "";
    public string? Civilite { get; init; }
    public string? Nom_contact { get; init; }
    public string? Prenom_contact { get; init; }
    public string? Telephone_portable { get; init; }
    public string? Telephone_fixe { get; init; }
    public string? Telephone_autre { get; init; }
    public string? E_mail { get; init; }
    public string? Remarque { get; init; }
    public double? Personne_physique { get; init; }
    public double? Adresse_entreprise { get; init; }
    public string? description_telephone { get; init; }
    public string? IDADRESSES { get; init; }
    public AdresseView? Adresse { get; init; }
    public List<ContactDetailSociete> SocieteContacts { get; init; } = [];
    public List<ContactDetailPoint> PointContacts { get; init; } = [];
}

public sealed record ContactDetailSociete(string? IDSOCIETES, string? Type_lien, string? Fonction_contact, string? Service_bureau, NomSociete? Societe);

public sealed record ContactDetailPoint(string? Lien, double? Recevoir_Mail_Planning, PointLibelle? Point);

public sealed record PointLibelle(string? Libelle);

// Adresse d'une société (GET /societes/:id, champ Adresse seulement).
public sealed record SocieteAdresse(SocieteAdresseLignes? Adresse);

public sealed record SocieteAdresseLignes(string? Adresse1, string? CP, string? Localite);
