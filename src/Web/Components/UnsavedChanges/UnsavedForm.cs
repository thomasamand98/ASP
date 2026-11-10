using System.Text.Json;
using Horizon.Shared;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Horizon.Web.Components.UnsavedChanges;

// à passer à <GuardBoundary Scope> et à détacher au Dispose
public static class GuardScopes
{
    public static GuardScope CreateChild(GuardScope? parent, UnsavedChangesService service)
    {
        var scope = new GuardScope(parent ?? service.Root);
        scope.Attach();
        return scope;
    }
}

// gardes personnalisés (éditeur de modèle, feuille de pointage...)
public sealed class LeaveGuard(Func<bool> isDirty, Func<Task> save, Func<bool>? validate = null) : ILeaveGuard, IDisposable
{
    private GuardScope? _scope;
    private bool _released;

    public bool IsDirty() => !_released && isDirty();

    public Task SaveAsync() => save();

    public bool Validate() => validate?.Invoke() ?? true;

    public void Release() => _released = true;

    public void Register(GuardScope scope)
    {
        if (_scope == scope) return;
        _scope?.Guards.Remove(this);
        _scope = scope;
        scope.Guards.Add(this);
    }

    public void Dispose() => _scope?.Guards.Remove(this);
}

// la référence suit la valeur jusqu'à la première interaction, pour ignorer les préremplissages
public sealed class UnsavedForm : ILeaveGuard, IDisposable
{
    private readonly Func<object?> _value;
    private readonly Func<Task> _save;
    private readonly UnsavedChangesService _service;
    private readonly Browser _browser;
    private DotNetObjectReference<UnsavedForm>? _self;
    private GuardScope? _scope;
    private ElementReference? _root;
    private string? _baseline;
    private bool _touched;
    private bool _released;

    public UnsavedForm(UnsavedChangesService service, Browser browser, Func<object?> value, Func<Task> save)
    {
        _service = service;
        _browser = browser;
        _value = value;
        _save = save;
    }

    private string Serialize() => JsonSerializer.Serialize(_value(), JsonConfig.Options);

    // à chaque OnParametersSet
    public void Register(GuardScope? scope)
    {
        scope ??= _service.Root;
        if (_scope == scope) return;
        _scope?.Guards.Remove(this);
        _scope = scope;
        scope.Guards.Add(this);
    }

    // au premier rendu, sert à détecter la première interaction
    public async Task TrackAsync(ElementReference root)
    {
        _root = root;
        _self ??= DotNetObjectReference.Create(this);
        await _browser.TrackTouchAsync(root, _self);
    }

    [JSInvokable]
    public void Touch()
    {
        if (_touched) return;
        _baseline = Serialize();
        _touched = true;
    }

    public bool IsDirty() => !_released && _touched && Serialize() != _baseline;

    public Task SaveAsync() => _save();

    public bool Validate() => _root is not { } root || _browser.ReportValidity(root);

    public void Release() => _released = true;

    public async Task ConfirmLeaveAsync(Func<Task> leave)
    {
        if (await _service.ConfirmLeaveAsync(LeaveTarget.Of(this))) await leave();
    }

    // fiche restée ouverte après enregistrement : nouvelle référence
    public async Task MarkCleanAsync()
    {
        _touched = false;
        _baseline = null;
        if (_root is { } root && _self is not null) await _browser.TrackTouchAsync(root, _self);
    }

    public void Dispose()
    {
        _scope?.Guards.Remove(this);
        _self?.Dispose();
    }
}
