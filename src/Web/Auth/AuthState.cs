namespace Horizon.Web.Auth;

// Session ouverte, fournie par AuthGate à toute l'application (paramètre en
// cascade) — disponible uniquement une fois la session ouverte.
public sealed class AuthState(string login, bool canSignOut, Func<Task> signOut)
{
    // Identifiant de l'utilisateur connecté.
    public string Login { get; } = login;

    // Faux tant que l'écran de connexion est désactivé : se déconnecter
    // rouvrirait aussitôt une session automatique, le bouton n'a alors pas de
    // sens.
    public bool CanSignOut { get; } = canSignOut;

    public Task SignOutAsync() => signOut();
}
