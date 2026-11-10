namespace Horizon.Shared.Models;

// Type : code énumération type_vehicule
public record Vehicule
{
    public string IDVEHICULES { get; init; } = "";
    public string? Num_immat { get; init; }
    public double? Type { get; init; }
    public string? Marque { get; init; }
    public string? Modele { get; init; }
    public string? IDSOCIETES { get; init; }
    public NomSociete? Societe { get; init; }
    public string? Num_police_assurance { get; init; }
    public string? Num_chassis { get; init; }
    public string? Date_validite_assurance { get; init; }
    public string? Num_licence_transport { get; init; }
    public string? Date_validite_licence { get; init; }
    public string? Date_inspection_auto { get; init; }
    public string? Date_validite_tachygeaphe { get; init; }
}

public sealed record VehiculeDetail : Vehicule
{
    public string? Date_modification_licence { get; init; }
    public string? Date_radiation_immatriculation { get; init; }
    public string? Date_vente { get; init; }
    public string? Date_premiere_mise_en_circulation { get; init; }
    public double Avec_compresseur { get; init; }
}
