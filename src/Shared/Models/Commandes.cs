namespace Horizon.Shared.Models;

// Marchandise et Unite repris de la prestation du contrat
public sealed record Commande
{
    public string IDCOMMANDES { get; init; } = "";
    public string? Date_commande { get; init; }
    public string? IDCONTRATS { get; init; }
    public CommandeContrat? Contrat { get; init; }
    public string? IDPRESTATIONS { get; init; }
    public PrestationDescription? Prestation { get; init; }
    public MarchandiseNom? Marchandise { get; init; }
    public double? Unite { get; init; }
    public double? QT { get; init; }
    public double? QT_planifie { get; init; }
    public string? Statut { get; init; }
    public string? NumRef { get; init; }
    public string? Instruction { get; init; }
}

public sealed record CommandeContrat(string? Num_contrat, string? Version_contrat, string? Date_debut, string? Date_fin, NomSociete? Societe);

public sealed record PrestationDescription(string? Description_prestation);

// GET /contrats/:id/prestations
public sealed record PrestationOption(string IDPRESTATIONS, string? Description_prestation, string? Description_courte);

public sealed record PrestationsResponse(List<PrestationOption> prestations);
