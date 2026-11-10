namespace Horizon.Shared.Models;

// Enum_system = 1 : non supprimable
public sealed record CategorieEnumeration(string IDCATEGORIES_ENUMERATION, string? Nom, string? Nom_affiche, double? Enum_system, CategorieEnumerationCount _count);

public sealed record CategorieEnumerationCount(int Enumerations);

public sealed record CategoriesEnumerationResponse(List<CategorieEnumeration> categories);

public sealed record Enumeration(string IDENUMERATIONS, string? IDCATEGORIES_ENUMERATION, string? Valeur_affiche, string? Valeur, double? Ordre, string? Valeur_associee, double? Valeur_system);

public sealed record EnumerationsResponse(List<Enumeration> enumerations);
