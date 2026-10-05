namespace Horizon.Shared.Models;

// Catégorie d'énumération (GET /categories-enumeration). Enum_system est un
// code 0/1 : 1 = catégorie utilisée par le code applicatif, non supprimable.
public sealed record CategorieEnumeration(string IDCATEGORIES_ENUMERATION, string? Nom, string? Nom_affiche, double? Enum_system, CategorieEnumerationCount _count);

public sealed record CategorieEnumerationCount(int Enumerations);

public sealed record CategoriesEnumerationResponse(List<CategorieEnumeration> categories);

// Valeur d'une catégorie (GET /enumerations). Valeur_system : code 0/1.
public sealed record Enumeration(string IDENUMERATIONS, string? IDCATEGORIES_ENUMERATION, string? Valeur_affiche, string? Valeur, double? Ordre, string? Valeur_associee, double? Valeur_system);

public sealed record EnumerationsResponse(List<Enumeration> enumerations);
