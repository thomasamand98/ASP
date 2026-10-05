using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Enumerations;

// ===== SERVICE =====
// Catégories d'énumérations (table categories_enumeration) : chacune regroupe
// une liste de valeurs (voir EnumerationService). `_count.Enumerations` évite
// au frontend un appel par catégorie pour afficher le nombre de valeurs dans
// la liste de gauche.
public class CategorieEnumerationService(AppDbContext db)
{
    public async Task<object> GetCategoriesEnumerationAsync(CancellationToken ct)
    {
        var categories = await Select(db.CategorieEnumeration.OrderBy(c => c.IDCATEGORIES_ENUMERATION)).ToListAsync(ct);
        return new { categories };
    }

    public async Task<object> CreateCategorieEnumerationAsync(CreateCategorieEnumerationDto dto, CancellationToken ct)
    {
        // IDUTILISATEURS_createur a un défaut DB de 0, qui viole sa contrainte
        // de clé étrangère quand il est omis — mis explicitement à NULL.
        var categorie = new CategorieEnumeration
        {
            IDUTILISATEURS_createur = null,
            Date_heure_creation = DateTime.UtcNow,
            Date_heure_modification = DateTime.UtcNow,
        };
        Assign(categorie, dto);
        db.CategorieEnumeration.Add(categorie);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(categorie.IDCATEGORIES_ENUMERATION, ct);
    }

    public async Task<object> UpdateCategorieEnumerationAsync(long id, UpdateCategorieEnumerationDto dto, CancellationToken ct)
    {
        var categorie = await db.CategorieEnumeration.Where(c => c.IDCATEGORIES_ENUMERATION == id).FirstOrThrowAsync(ct);
        Assign(categorie, dto);
        categorie.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ViewAsync(id, ct);
    }

    // Refuse de supprimer une catégorie système ou encore utilisée : les
    // énumérations qui y sont liées perdraient leur rattachement (clé
    // étrangère) et le code applicatif s'appuie sur les catégories système.
    public async Task DeleteCategorieEnumerationAsync(long id, CancellationToken ct)
    {
        var categorie = await db.CategorieEnumeration
            .Where(c => c.IDCATEGORIES_ENUMERATION == id)
            .Select(c => new { c.Enum_system, Count = c.Enumerations.Count })
            .FirstOrThrowAsync(ct);
        if (categorie.Enum_system is not (null or 0)) throw new ConflictException("Une catégorie système ne peut pas être supprimée");
        if (categorie.Count > 0) throw new ConflictException("Supprimez d’abord les énumérations de cette catégorie");
        await db.CategorieEnumeration.Where(c => c.IDCATEGORIES_ENUMERATION == id).DeleteOrThrowAsync(ct);
    }

    private static void Assign(CategorieEnumeration c, CreateCategorieEnumerationDto dto)
    {
        dto.Nom.Apply(v => c.Nom = v);
        dto.Nom_affiche.Apply(v => c.Nom_affiche = v);
        dto.Enum_system.ApplyInt(v => c.Enum_system = v);
    }

    private static IQueryable<object> Select(IQueryable<CategorieEnumeration> query) =>
        query.Select(c => new { c.IDCATEGORIES_ENUMERATION, c.Nom, c.Nom_affiche, c.Enum_system, _count = new { Enumerations = c.Enumerations.Count } });

    private Task<object> ViewAsync(long id, CancellationToken ct) =>
        Select(db.CategorieEnumeration.Where(c => c.IDCATEGORIES_ENUMERATION == id)).FirstOrThrowAsync(ct);
}

// ===== CONTROLLER =====
public class CategorieEnumerationController(CategorieEnumerationService service) : ApiControllerBase
{
    // NestJS : GET categories-enumeration
    [HttpGet("categories-enumeration")]
    public Task<object> GetCategoriesEnumeration(CancellationToken ct) => service.GetCategoriesEnumerationAsync(ct);

    // NestJS : POST categories-enumeration
    [HttpPost("categories-enumeration")]
    public Task<IActionResult> CreateCategorieEnumeration([Body] CreateCategorieEnumerationDto dto, CancellationToken ct) =>
        Created201(service.CreateCategorieEnumerationAsync(dto, ct));

    // NestJS : PATCH categories-enumeration/:id
    [HttpPatch("categories-enumeration/{id}")]
    public Task<object> UpdateCategorieEnumeration(string id, [Body] UpdateCategorieEnumerationDto dto, CancellationToken ct) =>
        service.UpdateCategorieEnumerationAsync(Params.ParseId(id), dto, ct);

    // NestJS : DELETE categories-enumeration/:id
    [HttpDelete("categories-enumeration/{id}")]
    public Task DeleteCategorieEnumeration(string id, CancellationToken ct) => service.DeleteCategorieEnumerationAsync(Params.ParseId(id), ct);
}
