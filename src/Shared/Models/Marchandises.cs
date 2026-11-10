namespace Horizon.Shared.Models;

// CouleurPlanning : couleur WinDev en texte
public record Marchandise
{
    public string IDMARCHANDISES { get; init; } = "";
    public string? Nom_marchandise { get; init; }
    public string? CouleurPlanning { get; init; }
    public string? IDDECHETS { get; init; }
    public MarchandiseDechet? Dechet { get; init; }
}

public sealed record MarchandiseDechet(string? Description_dechet, string? Code, double? Dangereux, double? Autorisation, double? Inerte, double? Menager);

public sealed record MarchandiseDetail : Marchandise
{
    public double? Is_dechet { get; init; }
    public double? Archive { get; init; }
}

// GET /dechets
public sealed record Dechet(string IDDECHETS, string? Code, string? Description_dechet);

public sealed record DechetsResponse(List<Dechet> dechets);
