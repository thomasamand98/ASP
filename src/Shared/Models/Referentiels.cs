namespace Horizon.Shared.Models;

// Réponses de l'API — référentiels (pays, villes, déchets, types de facture,
// messages envoyés).
public sealed record PaysOption(string IDPAYS, string? ISO, string? Nom);

public sealed record PaysResponse(List<PaysOption> pays);

public sealed record VilleOption(string IDVILLES, string? Nom_ville);

public sealed record VillesResponse(List<VilleOption> villes);

// Adresse résolue telle que renvoyée par l'API (champs NULL possibles).
public sealed record AdresseView(string? Adresse1, string? Adresse2, string? Adresse3, string? CP, string? Localite, string? Pays, string? Pays_full_name);
