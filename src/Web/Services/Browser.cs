using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Horizon.Web.Services;

// Fonctions du navigateur sans équivalent Blazor (voir wwwroot/js/interop.js).
public sealed class Browser(IJSRuntime js)
{
    // ---------- Stockage local ----------
    public ValueTask<string?> GetItemAsync(string key) => js.InvokeAsync<string?>("horizon.storage.get", key);
    public ValueTask SetItemAsync(string key, string value) => js.InvokeVoidAsync("horizon.storage.set", key, value);
    public ValueTask RemoveItemAsync(string key) => js.InvokeVoidAsync("horizon.storage.remove", key);

    // Lecture synchrone : possible en WebAssembly, utilisée pour l'état
    // initial d'un composant (onglets ouverts, préférences) sans rendu
    // intermédiaire, comme les useState(() => localStorage...) d'origine.
    public string? GetItem(string key) => ((IJSInProcessRuntime)js).Invoke<string?>("horizon.storage.get", key);

    public ValueTask ApplyThemeAsync(string preference) => js.InvokeVoidAsync("horizon.applyTheme", preference);

    // ---------- Boîtes natives ----------
    public ValueTask<bool> ConfirmAsync(string message) => js.InvokeAsync<bool>("horizon.confirm", message);
    public ValueTask AlertAsync(string message) => js.InvokeVoidAsync("horizon.alert", message);

    // ---------- Fichiers et fenêtres ----------
    public ValueTask DownloadAsync(string fileName, byte[] bytes, string? mime = null) =>
        js.InvokeVoidAsync("horizon.download", fileName, bytes, mime);

    // 0 si le navigateur a bloqué la fenêtre.
    public int OpenBlankWindow() => ((IJSInProcessRuntime)js).Invoke<int>("horizon.openBlankWindow");
    public ValueTask ShowInWindowAsync(int windowId, byte[] bytes, string? mime) => js.InvokeVoidAsync("horizon.showInWindow", windowId, bytes, mime);
    public ValueTask CloseWindowAsync(int windowId) => js.InvokeVoidAsync("horizon.closeWindow", windowId);
    public ValueTask<string> CreateObjectUrlAsync(byte[] bytes, string? mime) => js.InvokeAsync<string>("horizon.createUrl", bytes, mime);
    public ValueTask RevokeObjectUrlAsync(string url) => js.InvokeVoidAsync("horizon.revokeUrl", url);

    // ---------- Éléments ----------
    public ValueTask FocusAsync(ElementReference element) => js.InvokeVoidAsync("horizon.focus", element);
    public ValueTask SetIndeterminateAsync(ElementReference element, bool value) => js.InvokeVoidAsync("horizon.setIndeterminate", element, value);
    public bool ReportValidity(ElementReference element) => ((IJSInProcessRuntime)js).Invoke<bool>("horizon.reportValidity", element);
    public ValueTask<ElementRect> GetRectAsync(ElementReference element) => js.InvokeAsync<ElementRect>("horizon.rect", element);
    public ValueTask TrackTouchAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeVoidAsync("horizon.trackTouch", element, target);

    // ---------- Écouteurs du document ----------
    // `target` expose [JSInvokable] bool OnEscape() (true : Échap consommé).
    public ValueTask<int> ListenEscapeAsync<T>(DotNetObjectReference<T> target, bool capture = false) where T : class =>
        js.InvokeAsync<int>("horizon.listenEscape", target, capture);

    // `target` expose [JSInvokable] Task OnOutside().
    public ValueTask<int> ListenOutsideAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeAsync<int>("horizon.listenOutside", element, target);

    public ValueTask UnlistenAsync(int id) => js.InvokeVoidAsync("horizon.unlisten", id);

    // `target` expose [JSInvokable] bool HasUnsavedChanges().
    public ValueTask GuardUnloadAsync<T>(DotNetObjectReference<T> target) where T : class => js.InvokeVoidAsync("horizon.guardUnload", target);

    public ValueTask LockScrollAsync(bool locked) => js.InvokeVoidAsync("horizon.lockScroll", locked);

    // `target` expose [JSInvokable] Task OnWidth(double).
    public ValueTask<int> ObserveWidthAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeAsync<int>("horizon.observeWidth", element, target);

    public ValueTask UnobserveAsync(int id) => js.InvokeVoidAsync("horizon.unobserve", id);
}

public sealed record ElementRect(double left, double top, double width, double height);
