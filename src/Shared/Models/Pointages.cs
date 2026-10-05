namespace Horizon.Shared.Models;

// Réponses de l'API — module Pointage (GET /pointage, /pointage/:id,
// /pointage/feuille, /pointage/statuts). Les colonnes TIME (Heure_*,
// Debut_pause...) arrivent comme un instant du 01/01/1970 en UTC : seules
// les heures et minutes comptent. Nuitee : code 0/1.
public sealed record PointagePersonnel(string? Nom_Personnel, string? Prenom_Personnel);

public sealed record StatutLibelle(string? Libelle_generique);

public record Pointage
{
    public string IDPOINTAGES { get; init; } = "";
    public string? Date_application { get; init; }
    public string? IDPERSONNELS { get; init; }
    public PointagePersonnel? Personnel { get; init; }
    public string? Date_heure_debut { get; init; }
    public string? Date_heure_fin { get; init; }
    public string? Heure_coupure { get; init; }
    public string? Heure_nuit { get; init; }
    public string? Heure_liaison { get; init; }
    public string Heure_Stanby { get; init; } = "";
    public string? IDType_Statut { get; init; }
    public StatutLibelle? TypeStatut { get; init; }
    public double? Nuitee { get; init; }
    public string? Remarque { get; init; }
}

public sealed record PointageDetail : Pointage
{
    public string? Debut_pause { get; init; }
    public string? Fin_Pause { get; init; }
    public string? Heure_jour { get; init; }
}

// Statut avec sa couleur WinDev et son caractère « travaillé ».
public sealed record FeuilleStatut(string? Libelle_generique, double? CouleurStatut, double? Statut_de_travail);

// Pointage de la feuille : fiche complète, plus la couleur et le caractère
// « travaillé » du statut.
public sealed record PointageFeuille
{
    public string IDPOINTAGES { get; init; } = "";
    public string? Date_application { get; init; }
    public string? IDPERSONNELS { get; init; }
    public PointagePersonnel? Personnel { get; init; }
    public string? Date_heure_debut { get; init; }
    public string? Date_heure_fin { get; init; }
    public string? Heure_coupure { get; init; }
    public string? Heure_nuit { get; init; }
    public string? Heure_liaison { get; init; }
    public string? Heure_Stanby { get; init; }
    public string? IDType_Statut { get; init; }
    public FeuilleStatut? TypeStatut { get; init; }
    public double? Nuitee { get; init; }
    public string? Remarque { get; init; }
    public string? Debut_pause { get; init; }
    public string? Fin_Pause { get; init; }
    public string? Heure_jour { get; init; }
}

// Taux horaire du contrat de travail en vigueur (Decimal en texte).
public sealed record FeuilleTaux(string? Date_application, string? Taux_horaire, string? Taux_horaire_supplementaire, string? Taux_horaire_nuitee,
    string? Taux_nuitee, string? Taux_samedi, string? Taux_cheque_repas, string? Taux_stanby);

public sealed record Feuille(List<PointageFeuille> pointages, FeuilleTaux? taux);

// Statut de pointage (table types_statut). Archive / Statut_de_travail : codes 0/1.
public sealed record TypeStatut(string IDType_Statut, string? Libelle_generique, double? CouleurStatut, double? Statut_de_travail, double? Archive);

public sealed record TypesStatutResponse(List<TypeStatut> statuts);
