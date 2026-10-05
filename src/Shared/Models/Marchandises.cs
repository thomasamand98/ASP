namespace Horizon.Shared.Models;

// Réponses de l'API — module Marchandises (GET /marchandises et
// /marchandises/:id). CouleurPlanning : couleur WinDev (BigInt, en texte) ;
// champs du déchet lié et Is_dechet/Archive : codes 0/1.
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

// Déchet tel que renvoyé par GET /dechets.
public sealed record Dechet(string IDDECHETS, string? Code, string? Description_dechet);

public sealed record DechetsResponse(List<Dechet> dechets);
