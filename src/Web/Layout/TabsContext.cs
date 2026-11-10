using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Layout;

// en cascade depuis AppLayout
public sealed class TabsContext(Action<string, string, RenderFragment> openOrActivate, Func<string, bool, Task> close)
{
    // réactive l'onglet `id` s'il existe déjà
    public void OpenOrActivateTab(string id, string label, RenderFragment content) => openOrActivate(id, label, content);

    // force : sans confirmation, ex. juste après un enregistrement
    public Task CloseTabAsync(string id, bool force = false) => close(id, force);
}
