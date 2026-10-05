namespace Horizon.Web.Components.UnsavedChanges;

// Suivi des saisies non enregistrées, commun à toute l'application.
//
// Chaque saisie modifiable (fiche, éditeur...) déclare un « garde »
// (ILeaveGuard) dans la zone (GuardScope) où elle est affichée. Les zones
// s'imbriquent comme l'interface : application > onglet > fiche > sous-fiche.
// Quitter une zone (fermer une fiche, un onglet, se déconnecter...) demande
// d'abord quoi faire des gardes modifiés qu'elle contient, à n'importe quelle
// profondeur — voir UnsavedChangesService pour le dialogue.
public interface ILeaveGuard
{
    bool IsDirty();

    // Enregistre la saisie. Réussi si, une fois terminé, le garde n'est plus
    // modifié ou a disparu (fiche refermée après enregistrement) ; sinon
    // l'enregistrement a échoué et la saisie reste affichée.
    Task SaveAsync();

    // Contrôle navigateur (champs requis, formats) avant d'enregistrer.
    bool Validate();

    // Modifications abandonnées : le garde ne bloque plus la sortie.
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

// Cible d'une demande de sortie : une zone entière, ou des gardes précis
// (ex. le bouton Annuler d'une fiche ne concerne que cette fiche).
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
