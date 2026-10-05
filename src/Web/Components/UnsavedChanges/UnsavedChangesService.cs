using Microsoft.JSInterop;

namespace Horizon.Web.Components.UnsavedChanges;

// Racine du suivi des saisies non enregistrées (voir GuardScope) : fournit la
// zone de l'application et la question « Enregistrer / Annuler les
// modifications » posée quand on quitte une zone modifiée (affichée par
// UnsavedChangesDialog). Prévient aussi le navigateur à la fermeture ou au
// rechargement de la page (voir HasUnsavedChanges).
public sealed class UnsavedChangesService
{
    private sealed record Pending(LeaveTarget Target, List<ILeaveGuard> Guards, TaskCompletionSource<bool> Result);

    private Pending? _pending;

    public GuardScope Root { get; } = new(null);

    public bool IsAsking => _pending is not null;

    public bool Saving { get; private set; }

    public event Action? Changed;

    // true = on peut quitter (rien de modifié, enregistré, ou abandonné).
    public Task<bool> ConfirmLeaveAsync(LeaveTarget target)
    {
        var guards = target.DirtyGuards();
        if (guards.Count == 0) return Task.FromResult(true);
        // Une seule question à la fois : une demande pendant qu'une autre est
        // affichée est refusée (double clic sur une croix, par exemple).
        if (_pending is not null) return Task.FromResult(false);
        _pending = new Pending(target, guards, new TaskCompletionSource<bool>());
        Changed?.Invoke();
        return _pending.Result.Task;
    }

    public void Finish(bool canLeave)
    {
        var request = _pending;
        _pending = null;
        Saving = false;
        Changed?.Invoke();
        request?.Result.TrySetResult(canLeave);
    }

    public async Task SaveAsync()
    {
        var request = _pending;
        if (request is null) return;
        // Saisie invalide : on reste sur la fiche, le navigateur signale le champ.
        if (!request.Guards.All(guard => guard.Validate()))
        {
            Finish(false);
            return;
        }
        Saving = true;
        Changed?.Invoke();
        try
        {
            foreach (var guard in request.Guards) await guard.SaveAsync();
        }
        catch (Exception)
        {
            // Erreur déjà affichée par la saisie elle-même.
        }
        Finish(await SettledAsync(request.Target));
    }

    public void Discard()
    {
        _pending?.Guards.ForEach(guard => guard.Release());
        Finish(true);
    }

    // Laisse l'interface appliquer les mises à jour déclenchées par
    // l'enregistrement (fiche refermée, état « propre ») avant de vérifier le
    // résultat.
    private static async Task<bool> SettledAsync(LeaveTarget target)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(16);
            if (target.DirtyGuards().Count == 0) return true;
        }
        return false;
    }

    [JSInvokable]
    public bool HasUnsavedChanges() => Root.DirtyGuards().Count > 0;
}
