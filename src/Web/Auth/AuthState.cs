namespace Horizon.Web.Auth;

// en cascade depuis AuthGate
public sealed class AuthState(string login, bool canSignOut, Func<Task> signOut)
{
    public string Login { get; } = login;

    // false sans écran de connexion, reconnexion auto immédiate sinon
    public bool CanSignOut { get; } = canSignOut;

    public Task SignOutAsync() => signOut();
}
