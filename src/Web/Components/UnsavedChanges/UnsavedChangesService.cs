using Microsoft.JSInterop;

namespace Horizon.Web.Components.UnsavedChanges;

// racine du suivi des saisies, prévient aussi à la fermeture de l'onglet
public sealed class UnsavedChangesService
{
    private sealed record Pending(LeaveTarget Target, List<ILeaveGuard> Guards, TaskCompletionSource<bool> Result);

    private Pending? _pending;

    public GuardScope Root { get; } = new(null);

    public bool IsAsking => _pending is not null;

    public bool Saving { get; private set; }

    public event Action? Changed;

    // true = on peut quitter
    public Task<bool> ConfirmLeaveAsync(LeaveTarget target)
    {
        var guards = target.DirtyGuards();
        if (guards.Count == 0) return Task.FromResult(true);
        // une seule question à la fois (double clic sur une croix...)
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
        // saisie invalide, le navigateur signale le champ
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
            // erreur déjà affichée par la saisie
        }
        Finish(await SettledAsync(request.Target));
    }

    public void Discard()
    {
        _pending?.Guards.ForEach(guard => guard.Release());
        Finish(true);
    }

    // laisse passer le rendu (fiche refermée...) avant de vérifier
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
