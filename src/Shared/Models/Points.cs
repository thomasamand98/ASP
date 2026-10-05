namespace Horizon.Shared.Models;

// Réponses de l'API — module Points (GET /points et /points/:id).
public sealed record Point
{
    public string IDPOINTS { get; init; } = "";
    public string? Libelle { get; init; }
    public string? Nom_societe { get; init; }
    public string? Telephone { get; init; }
    public string? IDADRESSES { get; init; }
    public string? IDSOCIETES { get; init; }
    public PointAdresse? Adresse { get; init; }
}

public sealed record PointAdresse(string? Adresse1, string? CP, string? Localite);

public sealed record PointDetail
{
    public string IDPOINTS { get; init; } = "";
    public string? Libelle { get; init; }
    public string? Nom_societe { get; init; }
    public string? Telephone { get; init; }
    public string? IDADRESSES { get; init; }
    public string? IDSOCIETES { get; init; }
    public AdresseView? Adresse { get; init; }
    public double? Archive { get; init; }
    public string? Lien_googleMap { get; init; }
    public string? Instruction { get; init; }
    public string? IDCONTACTS_DEFAUTS { get; init; }
    public SocieteContactNom? ContactDefauts { get; init; }
    public List<PointContact> PointContacts { get; init; } = [];
    // Plages d'ouverture (table grilles_horaires) : Jour_semaine 1 = lundi …
    // 7 = dimanche, heures en « HH:MM ».
    public List<PointHoraire> Horaires { get; init; } = [];
}

public sealed record PointContact(string? IDCONTACTS, string? Lien, double? Recevoir_Mail_Planning, PointContactFiche? Contact);

public sealed record PointContactFiche(string? Civilite, string? Nom_contact, string? Prenom_contact, string? Telephone_portable, string? Telephone_fixe, string? E_mail);

public sealed record PointHoraire(int Jour_semaine, string Heure_debut, string Heure_fin);
