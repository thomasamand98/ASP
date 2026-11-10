using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.DocumentTemplates;

public class DocumentTemplateService(AppDbContext db)
{
    private const string NotFoundMessage = "Modèle de document introuvable";

    // sans le HTML, trop lourd
    public async Task<object> GetDocumentTemplatesAsync(string? type, CancellationToken ct)
    {
        IQueryable<DocumentTemplate> query = db.DocumentTemplate;
        if (!string.IsNullOrEmpty(type)) query = query.Where(t => t.Type_document == type);
        var documentTemplates = await query.OrderBy(t => t.Nom)
            .Select(t => new { t.IDDOCUMENT_TEMPLATES, t.Nom, t.Type_document, t.Description, t.Date_heure_creation, t.Date_heure_modification })
            .ToListAsync(ct);
        return new { documentTemplates };
    }

    public async Task<object> GetDocumentTemplateAsync(long id, CancellationToken ct) =>
        await DetailAsync(id, ct) ?? throw new NotFoundException(NotFoundMessage);

    public async Task<DocumentTemplateContent> GetDocumentTemplateContentAsync(long id, CancellationToken ct) =>
        await db.DocumentTemplate.Where(t => t.IDDOCUMENT_TEMPLATES == id)
            .Select(t => new DocumentTemplateContent(t.Contenu_entete, t.Contenu_html, t.Contenu_pied))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(NotFoundMessage);

    public async Task<object> CreateDocumentTemplateAsync(CreateDocumentTemplateDto dto, CancellationToken ct)
    {
        // défaut DB à 0 (FK)
        var template = new DocumentTemplate
        {
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = DateTime.UtcNow,
            Date_heure_modification = DateTime.UtcNow,
        };
        Assign(template, dto);
        db.DocumentTemplate.Add(template);
        await db.SaveChangesAsync(ct);
        return (await DetailAsync(template.IDDOCUMENT_TEMPLATES, ct))!;
    }

    public async Task<object> UpdateDocumentTemplateAsync(long id, UpdateDocumentTemplateDto dto, CancellationToken ct)
    {
        var template = await db.DocumentTemplate.Where(t => t.IDDOCUMENT_TEMPLATES == id).FirstOrThrowAsync(ct);
        Assign(template, dto);
        template.Date_heure_modification = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await DetailAsync(id, ct))!;
    }

    public Task DeleteDocumentTemplateAsync(long id, CancellationToken ct) =>
        db.DocumentTemplate.Where(t => t.IDDOCUMENT_TEMPLATES == id).DeleteOrThrowAsync(ct);

    private static void Assign(DocumentTemplate t, CreateDocumentTemplateDto dto)
    {
        dto.Nom.Apply(v => t.Nom = v);
        dto.Type_document.Apply(v => t.Type_document = v);
        dto.Description.Apply(v => t.Description = v);
        dto.Contenu_entete.Apply(v => t.Contenu_entete = v);
        dto.Contenu_html.Apply(v => t.Contenu_html = v);
        dto.Contenu_pied.Apply(v => t.Contenu_pied = v);
    }

    private Task<object?> DetailAsync(long id, CancellationToken ct) =>
        db.DocumentTemplate.Where(t => t.IDDOCUMENT_TEMPLATES == id)
            .Select(t => (object)new
            {
                t.IDDOCUMENT_TEMPLATES,
                t.Nom,
                t.Type_document,
                t.Description,
                t.Date_heure_creation,
                t.Date_heure_modification,
                t.Contenu_entete,
                t.Contenu_html,
                t.Contenu_pied,
            })
            .FirstOrDefaultAsync(ct);
}

public class DocumentTemplateController(DocumentTemplateService service) : ApiControllerBase
{
    [HttpGet("document-templates")]
    public Task<object> GetDocumentTemplates([FromQuery] string? type, CancellationToken ct) => service.GetDocumentTemplatesAsync(type, ct);

    [HttpGet("document-templates/{id}")]
    public Task<object> GetDocumentTemplate(string id, CancellationToken ct) => service.GetDocumentTemplateAsync(Params.ParseId(id), ct);

    [HttpPost("document-templates")]
    public Task<IActionResult> CreateDocumentTemplate([Body] CreateDocumentTemplateDto dto, CancellationToken ct) =>
        Created201(service.CreateDocumentTemplateAsync(dto, ct));

    [HttpPatch("document-templates/{id}")]
    public Task<object> UpdateDocumentTemplate(string id, [Body] UpdateDocumentTemplateDto dto, CancellationToken ct) =>
        service.UpdateDocumentTemplateAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("document-templates/{id}")]
    public Task DeleteDocumentTemplate(string id, CancellationToken ct) => service.DeleteDocumentTemplateAsync(Params.ParseId(id), ct);
}
