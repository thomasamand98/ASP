namespace Horizon.Shared.Models;

public sealed record Tache(string IDTACHES, double? Type, string? Libelle, string? Date_tache, string? Idlien);

public sealed record TachesResponse(List<Tache> taches);
