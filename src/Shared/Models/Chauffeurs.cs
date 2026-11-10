namespace Horizon.Shared.Models;

public record Chauffeur
{
    public string IDCHAUFFEURS { get; init; } = "";
    public string? Nom_chauffeur { get; init; }
    public string? Categorie { get; init; }
    public string? Telephone { get; init; }
    public double? Archive { get; init; }
    public string? IDSOCIETES { get; init; }
    public NomSociete? Societe { get; init; }
    public string? IDPERSONNELS { get; init; }
    public ChauffeurPersonnel? Personnel { get; init; }
}

public sealed record ChauffeurPersonnel(string? Nom_Personnel, string? Prenom_Personnel, string? E_mail_professionnel);
