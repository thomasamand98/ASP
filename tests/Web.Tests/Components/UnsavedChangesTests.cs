using Bunit;
using Horizon.Web.Components.UnsavedChanges;
using Horizon.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Horizon.Web.Tests.Components;

// garde modifs non enregistrées : service + dialogue
public class UnsavedChangesTests : BunitContext
{
    private readonly UnsavedChangesService _service = new();
    private readonly GuardScope _scope;
    private bool _dirty;
    private int _saves;
    private bool _saveSucceeds = true;
    private readonly LeaveGuard _guard;

    public UnsavedChangesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_service);
        Services.AddSingleton<Browser>();
        _scope = GuardScopes.CreateChild(_service.Root, _service);
        _guard = new LeaveGuard(() => _dirty, () =>
        {
            _saves++;
            if (_saveSucceeds) _dirty = false;
            return Task.CompletedTask;
        });
        _guard.Register(_scope);
    }

    [Fact]
    public async Task Sans_modification_on_quitte_sans_dialogue()
    {
        var dialog = Render<UnsavedChangesDialog>();
        Assert.True(await _service.ConfirmLeaveAsync(_scope));
        Assert.Empty(dialog.FindAll(".unsaved-dialog"));
    }

    [Fact]
    public async Task Annuler_les_modifications_libere_la_saisie()
    {
        _dirty = true;
        var dialog = Render<UnsavedChangesDialog>();
        var leave = _service.ConfirmLeaveAsync(_scope);
        dialog.WaitForElement(".unsaved-dialog");
        dialog.FindAll(".unsaved-actions button")[0].Click();
        Assert.True(await leave);
        Assert.Equal(0, _saves);
        // garde abandonné : ne bloque plus
        Assert.False(_guard.IsDirty());
    }

    [Fact]
    public async Task Enregistrer_appelle_la_sauvegarde_puis_laisse_quitter()
    {
        _dirty = true;
        var dialog = Render<UnsavedChangesDialog>();
        var leave = _service.ConfirmLeaveAsync(_scope);
        dialog.WaitForElement(".unsaved-dialog");
        dialog.Find(".unsaved-actions .btn.primary").Click();
        Assert.True(await leave);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public async Task Un_enregistrement_qui_echoue_garde_la_saisie_affichee()
    {
        _dirty = true;
        _saveSucceeds = false;
        var dialog = Render<UnsavedChangesDialog>();
        var leave = _service.ConfirmLeaveAsync(_scope);
        dialog.WaitForElement(".unsaved-dialog");
        dialog.Find(".unsaved-actions .btn.primary").Click();
        Assert.False(await leave);
    }

    [Fact]
    public async Task Clic_sur_le_fond_reste_sur_la_saisie()
    {
        _dirty = true;
        var dialog = Render<UnsavedChangesDialog>();
        var leave = _service.ConfirmLeaveAsync(_scope);
        dialog.WaitForElement(".unsaved-overlay").Click();
        Assert.False(await leave);
        Assert.True(_guard.IsDirty());
    }

    [Fact]
    public async Task Une_zone_parente_voit_les_saisies_des_zones_enfants()
    {
        _dirty = true;
        Render<UnsavedChangesDialog>();
        var leave = _service.ConfirmLeaveAsync(_service.Root);
        Assert.True(_service.IsAsking);
        _service.Finish(false);
        Assert.False(await leave);
    }
}
