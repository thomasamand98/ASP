namespace Horizon.Shared.Models;

public sealed record PaysOption(string IDPAYS, string? ISO, string? Nom);

public sealed record PaysResponse(List<PaysOption> pays);

public sealed record VilleOption(string IDVILLES, string? Nom_ville);

public sealed record VillesResponse(List<VilleOption> villes);

// adresse résolue, champs NULL possibles
public sealed record AdresseView(string? Adresse1, string? Adresse2, string? Adresse3, string? CP, string? Localite, string? Pays, string? Pays_full_name);
