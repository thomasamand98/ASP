using System.Text.Json;
using Horizon.Shared;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Horizon.Web.Components.UnsavedChanges;

// Zone enfant de `parent`, créée par un composant qui doit pouvoir la
// vérifier avant de la quitter (fiche ouverte, onglet, modale) : à passer à
// <GuardBoundary Scope="..."> et à détacher quand le composant disparaît.
public static class GuardScopes
{
    public static GuardScope CreateChild(GuardScope? parent, UnsavedChangesService service)
    {
        var scope = new GuardScope(parent ?? service.Root);
        scope.Attach();
        return scope;
    }
}

// Saisie déclarée dans une zone (équivalent de useLeaveGuard) : gardes
// personnalisés (éditeur de modèle, feuille de pointage...).
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

// Version formulaire (équivalent de useUnsavedForm) : compare la valeur
// saisie (`value`, sérialisée) à celle de référence. Tant que l'utilisateur
// n'a pas interagi avec la fiche (saisie, clic, touche dans l'élément
// suivi), la référence suit la valeur : les pré-remplissages asynchrones
// (société par défaut, données chargées après ouverture...) ne comptent donc
// pas comme des modifications.
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

    // À appeler à chaque rendu (OnParametersSet) : rattache le garde à la
    // zone courante.
    public void Register(GuardScope? scope)
    {
        scope ??= _service.Root;
        if (_scope == scope) return;
        _scope?.Guards.Remove(this);
        _scope = scope;
        scope.Guards.Add(this);
    }

    // Élément racine de la fiche (le <form>), pour détecter la première
    // interaction et lancer la validation native. À appeler au premier rendu.
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

    // Boutons Annuler/Fermer de la fiche elle-même, qui ne concernent qu'elle.
    public async Task ConfirmLeaveAsync(Func<Task> leave)
    {
        if (await _service.ConfirmLeaveAsync(LeaveTarget.Of(this))) await leave();
    }

    // Après un enregistrement qui laisse la fiche ouverte : la valeur
    // enregistrée devient la référence (et les mises à jour renvoyées par le
    // serveur ne comptent pas comme des modifications).
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
