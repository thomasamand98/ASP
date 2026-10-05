namespace Horizon.Shared.Models;

// Réponses de l'API — module Tâches (GET /taches/a-faire).
public sealed record Tache(string IDTACHES, double? Type, string? Libelle, string? Date_tache, string? Idlien);

public sealed record TachesResponse(List<Tache> taches);
