namespace Horizon.Shared.Models;

// Sous-objets renvoyés par l'API dans plusieurs réponses (relations
// réduites à leur libellé).
public sealed record NomSociete(string? Nom_societe);
