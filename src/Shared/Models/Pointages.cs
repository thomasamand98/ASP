namespace Horizon.Shared.Models;

// colonnes TIME = 01/01/1970 UTC, seules h:mm comptent
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

// statut + couleur WinDev + travaillé ou non
public sealed record FeuilleStatut(string? Libelle_generique, double? CouleurStatut, double? Statut_de_travail);

// pointage complet + infos du statut
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

// taux du contrat en vigueur, en texte
public sealed record FeuilleTaux(string? Date_application, string? Taux_horaire, string? Taux_horaire_supplementaire, string? Taux_horaire_nuitee,
    string? Taux_nuitee, string? Taux_samedi, string? Taux_cheque_repas, string? Taux_stanby);

public sealed record Feuille(List<PointageFeuille> pointages, FeuilleTaux? taux);

// table types_statut
public sealed record TypeStatut(string IDType_Statut, string? Libelle_generique, double? CouleurStatut, double? Statut_de_travail, double? Archive);

public sealed record TypesStatutResponse(List<TypeStatut> statuts);
