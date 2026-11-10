namespace Horizon.Web.Components.UnsavedChanges;

// zones imbriquées : application > onglet > fiche > sous-fiche
public interface ILeaveGuard
{
    bool IsDirty();

    // réussi si le garde n'est plus modifié ou a disparu
    Task SaveAsync();

    // validation native du navigateur
    bool Validate();

    void Release();
}

public sealed class GuardScope(GuardScope? parent)
{
    public HashSet<ILeaveGuard> Guards { get; } = [];
    public HashSet<GuardScope> Children { get; } = [];
    public GuardScope? Parent { get; } = parent;

    public void Attach() => Parent?.Children.Add(this);

    public void Detach() => Parent?.Children.Remove(this);

    public List<ILeaveGuard> DirtyGuards() =>
        [.. Guards.Where(guard => guard.IsDirty()), .. Children.ToList().SelectMany(child => child.DirtyGuards())];
}

// zone entière ou gardes précis (bouton Annuler d'une fiche)
public sealed class LeaveTarget
{
    private readonly GuardScope? _scope;
    private readonly IReadOnlyList<ILeaveGuard>? _guards;

    private LeaveTarget(GuardScope? scope, IReadOnlyList<ILeaveGuard>? guards)
    {
        _scope = scope;
        _guards = guards;
    }

    public static implicit operator LeaveTarget(GuardScope scope) => new(scope, null);

    public static LeaveTarget Of(params ILeaveGuard[] guards) => new(null, guards);

    public List<ILeaveGuard> DirtyGuards() => _scope?.DirtyGuards() ?? [.. _guards!.Where(guard => guard.IsDirty())];
}
