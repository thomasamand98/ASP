using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Horizon.Web.Features.Documents;

// Enveloppe des fonctions `horizon.docEditor` (voir wwwroot/js/interop.js) :
// manipulation du DOM des zones contentEditable de l'éditeur de modèles de
// document, qui doit rester en JavaScript (Range/Selection, glisser-déposer
// HTML5) — voir DocumentTemplateEditor.razor.
public sealed class DocEditorJs(IJSRuntime js)
{
    public ValueTask RegisterZoneAsync<T>(ElementReference el, string key, DotNetObjectReference<T> target) where T : class =>
        js.InvokeVoidAsync("horizon.docEditor.registerZone", el, key, target);

    public ValueTask UnregisterZoneAsync(string key) => js.InvokeVoidAsync("horizon.docEditor.unregisterZone", key);

    public ValueTask RegisterCodeZoneAsync<T>(ElementReference el, string key, DotNetObjectReference<T> target) where T : class =>
        js.InvokeVoidAsync("horizon.docEditor.registerCodeZone", el, key, target);

    public ValueTask TrackSelectionAsync<T>(DotNetObjectReference<T> target) where T : class =>
        js.InvokeVoidAsync("horizon.docEditor.trackSelection", target);

    public ValueTask UntrackSelectionAsync() => js.InvokeVoidAsync("horizon.docEditor.untrackSelection");

    public ValueTask SetZoneHtmlAsync(ElementReference el, string html) => js.InvokeVoidAsync("horizon.docEditor.setZoneHtml", el, html);

    public ValueTask<string> GetZoneHtmlAsync(ElementReference el) => js.InvokeAsync<string>("horizon.docEditor.getZoneHtml", el);

    public ValueTask ExecAsync(string key, string command, string? value = null) => js.InvokeVoidAsync("horizon.docEditor.exec", key, command, value);

    public ValueTask InsertFieldAsync(string key, MergeField field) => js.InvokeVoidAsync("horizon.docEditor.insertField", key, field);

    public ValueTask InsertFieldAtPointAsync(string key, MergeField field, double x, double y) =>
        js.InvokeVoidAsync("horizon.docEditor.insertFieldAtPoint", key, field, x, y);

    public ValueTask InsertGroupAsync(string key, RepeatGroupPayload group) => js.InvokeVoidAsync("horizon.docEditor.insertGroup", key, group);

    public ValueTask InsertGroupAtPointAsync(string key, RepeatGroupPayload group, double x, double y) =>
        js.InvokeVoidAsync("horizon.docEditor.insertGroupAtPoint", key, group, x, y);

    public ValueTask InsertFreeTableAsync(string key) => js.InvokeVoidAsync("horizon.docEditor.insertFreeTable", key);

    public ValueTask InsertPageBreakAsync(string key) => js.InvokeVoidAsync("horizon.docEditor.insertPageBreak", key);

    public ValueTask InsertPageNumberAsync(string key) => js.InvokeVoidAsync("horizon.docEditor.insertPageNumber", key);

    public ValueTask<int[]> GetTextareaSelectionAsync(ElementReference el) => js.InvokeAsync<int[]>("horizon.docEditor.getTextareaSelection", el);

    public ValueTask FocusAndSelectAsync(ElementReference el, int start, int end) => js.InvokeVoidAsync("horizon.docEditor.focusAndSelect", el, start, end);
}

// Groupe de type liste sérialisé pour JS : colonnes déjà résolues côté C#
// (voir MergeFieldGroup.Columns), pour que interop.js n'ait pas à reproduire
// la logique de secours (colonnes par défaut, 4 premiers champs...).
public sealed record RepeatGroupPayload(string Label, string ListPath, IReadOnlyList<MergeField> Columns);
