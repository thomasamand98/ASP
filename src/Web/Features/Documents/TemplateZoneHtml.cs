namespace Horizon.Web.Features.Documents;

// Le HTML des trois zones de l'éditeur (voir DocumentTemplateEditor), tel
// qu'échangé avec la page qui l'héberge (DocumentTemplatesPage) à
// l'ouverture et à l'enregistrement d'un modèle.
public sealed record TemplateZoneHtml(string HeaderHtml, string ContentHtml, string FooterHtml);
