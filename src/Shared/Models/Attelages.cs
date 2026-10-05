namespace Horizon.Shared.Models;

// Réponses de l'API — module Attelages (GET /attelages) : attelages de
// référence (table attelages_reference), complétés des véhicules.
public sealed record AttelageVehicule(string? Marque, string? Modele, string? Num_immat);

public sealed record AttelageChauffeur(string? Nom_chauffeur);

public sealed record Attelage
{
    public string IDATTELAGE_REFERENCE { get; init; } = "";
    public string? IDCHAUFFEUR { get; init; }
    public AttelageChauffeur? Chauffeur { get; init; }
    public string? IDTRACTEUR { get; init; }
    public AttelageVehicule? Tracteur { get; init; }
    public string? IDREMORQUE { get; init; }
    public AttelageVehicule? Remorque { get; init; }
    public string? IDSOCIETES { get; init; }
    public NomSociete? Societe { get; init; }
}
