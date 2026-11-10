namespace Horizon.Web.Features.Documents;

// même balisage que createChipElement/createRepeatBlockElement côté JS
public static class TemplateChipHtml
{
    public static string Chip(MergeField field) => field.Kind == "image"
        ? $"""<span class="dte-chip dte-chip-image" contenteditable="false" data-merge-field="{field.Path}" data-kind="image"><span class="dte-chip-image-icon" aria-hidden="true">🖼</span><span class="dte-chip-label">{field.Label}</span><button type="button" class="dte-chip-remove" data-chip-remove aria-label="Retirer le champ {field.Label}">×</button></span>"""
        : $"""<span class="dte-chip" contenteditable="false" data-merge-field="{field.Path}" data-kind="{field.Kind}"><span class="dte-chip-label">{field.Label}</span><button type="button" class="dte-chip-remove" data-chip-remove aria-label="Retirer le champ {field.Label}">×</button></span>""";

    public static string RepeatTag(string label) =>
        $"""<div class="dte-repeat-tag" contenteditable="false"><span>↻ Répété pour chaque « {label} »</span><button type="button" class="dte-repeat-remove" data-repeat-remove aria-label="Supprimer le bloc « {label} »">× Supprimer le bloc</button></div>""";

    public static string RepeatBlock(MergeFieldGroup group, IReadOnlyList<MergeField> columns)
    {
        if (columns.Count == 0) return "";
        var headers = string.Concat(columns.Select(f => $"<th>{f.Label}</th>"));
        var cells = string.Concat(columns.Select(f => $"<td>{Chip(f)}</td>"));
        return $"""<div class="dte-repeat" data-repeat-list="{group.ListPath}" data-repeat-label="{group.Label}">{RepeatTag(group.Label)}<table class="dte-table"><thead><tr>{headers}</tr></thead><tbody class="dte-repeat-row"><tr>{cells}</tr></tbody></table></div>""";
    }

    public static string FreeTable() =>
        """<table class="dte-table dte-table-free"><tbody><tr><td><br></td><td><br></td></tr><tr><td><br></td><td><br></td></tr></tbody></table>""";

    public static string PageBreak() => """<div class="dte-page-break" contenteditable="false"><span>Saut de page</span></div>""";

    public static string PageNumber() => """<span class="dte-chip dte-chip-pagenum" contenteditable="false">Page <span class="pageNumber"></span> / <span class="totalPages"></span></span>""";
}
