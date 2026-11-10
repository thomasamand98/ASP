using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Horizon.Web.Services;

// wrappers de wwwroot/js/interop.js
public sealed class Browser(IJSRuntime js)
{
    public ValueTask<string?> GetItemAsync(string key) => js.InvokeAsync<string?>("horizon.storage.get", key);
    public ValueTask SetItemAsync(string key, string value) => js.InvokeVoidAsync("horizon.storage.set", key, value);
    public ValueTask RemoveItemAsync(string key) => js.InvokeVoidAsync("horizon.storage.remove", key);

    // synchrone pour l'état initial, évite un rendu intermédiaire
    public string? GetItem(string key) => ((IJSInProcessRuntime)js).Invoke<string?>("horizon.storage.get", key);

    public ValueTask ApplyThemeAsync(string preference) => js.InvokeVoidAsync("horizon.applyTheme", preference);

    public ValueTask<bool> ConfirmAsync(string message) => js.InvokeAsync<bool>("horizon.confirm", message);
    public ValueTask AlertAsync(string message) => js.InvokeVoidAsync("horizon.alert", message);

    public ValueTask DownloadAsync(string fileName, byte[] bytes, string? mime = null) =>
        js.InvokeVoidAsync("horizon.download", fileName, bytes, mime);

    // 0 si popup bloquée
    public int OpenBlankWindow() => ((IJSInProcessRuntime)js).Invoke<int>("horizon.openBlankWindow");
    public ValueTask ShowInWindowAsync(int windowId, byte[] bytes, string? mime) => js.InvokeVoidAsync("horizon.showInWindow", windowId, bytes, mime);
    public ValueTask CloseWindowAsync(int windowId) => js.InvokeVoidAsync("horizon.closeWindow", windowId);
    public ValueTask<string> CreateObjectUrlAsync(byte[] bytes, string? mime) => js.InvokeAsync<string>("horizon.createUrl", bytes, mime);
    public ValueTask RevokeObjectUrlAsync(string url) => js.InvokeVoidAsync("horizon.revokeUrl", url);
    public ValueTask CopyTextAsync(string text) => js.InvokeVoidAsync("horizon.copyText", text);

    public ValueTask FocusAsync(ElementReference element) => js.InvokeVoidAsync("horizon.focus", element);
    public ValueTask ClickAsync(ElementReference element) => js.InvokeVoidAsync("horizon.click", element);
    public ValueTask ClearValueAsync(ElementReference element) => js.InvokeVoidAsync("horizon.clearValue", element);
    public ValueTask SetIndeterminateAsync(ElementReference element, bool value) => js.InvokeVoidAsync("horizon.setIndeterminate", element, value);
    public bool ReportValidity(ElementReference element) => ((IJSInProcessRuntime)js).Invoke<bool>("horizon.reportValidity", element);
    public ValueTask<ElementRect> GetRectAsync(ElementReference element) => js.InvokeAsync<ElementRect>("horizon.rect", element);

    // synchrone : position requise avant la fin de l'événement de drag
    public ElementRect GetRect(ElementReference element) => ((IJSInProcessRuntime)js).Invoke<ElementRect>("horizon.rectSync", element);

    public ValueTask ScrollTopAsync(ElementReference element, double top) => js.InvokeVoidAsync("horizon.scrollTop", element, top);
    public ValueTask CapturePointerAsync(ElementReference element, long pointerId) => js.InvokeVoidAsync("horizon.capturePointer", element, pointerId);
    public ValueTask TrackTouchAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeVoidAsync("horizon.trackTouch", element, target);

    // target : [JSInvokable] bool OnEscape(), true si consommé
    public ValueTask<int> ListenEscapeAsync<T>(DotNetObjectReference<T> target, bool capture = false) where T : class =>
        js.InvokeAsync<int>("horizon.listenEscape", target, capture);

    // target : [JSInvokable] Task OnOutside()
    public ValueTask<int> ListenOutsideAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeAsync<int>("horizon.listenOutside", element, target);

    public ValueTask UnlistenAsync(int id) => js.InvokeVoidAsync("horizon.unlisten", id);

    // target : [JSInvokable] bool HasUnsavedChanges()
    public ValueTask GuardUnloadAsync<T>(DotNetObjectReference<T> target) where T : class => js.InvokeVoidAsync("horizon.guardUnload", target);

    public ValueTask LockScrollAsync(bool locked) => js.InvokeVoidAsync("horizon.lockScroll", locked);

    // target : [JSInvokable] Task OnWidth(double)
    public ValueTask<int> ObserveWidthAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class =>
        js.InvokeAsync<int>("horizon.observeWidth", element, target);

    public ValueTask<double[]> MeasureTextsAsync(string[] texts, double fontSize) => js.InvokeAsync<double[]>("horizon.measureTexts", texts, fontSize);

    public ValueTask UnobserveAsync(int id) => js.InvokeVoidAsync("horizon.unobserve", id);
}

public sealed record ElementRect(double left, double top, double width, double height);
