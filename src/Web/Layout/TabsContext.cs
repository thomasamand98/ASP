using Microsoft.AspNetCore.Components;

namespace Horizon.Web.Layout;

// Onglets de l'application, fournis par AppLayout à toutes les pages
// (paramètre en cascade).
public sealed class TabsContext(Action<string, string, RenderFragment> openOrActivate, Func<string, bool, Task> close)
{
    // Ouvre un nouvel onglet dans la barre globale (voir TabBar), ou réactive
    // celui d'id `id` s'il est déjà ouvert (jamais de doublon) — utilisé par
    // le mode « Onglet » de CrudPage pour ouvrir la fiche d'une ligne dans son
    // propre onglet, et par les projections.
    public void OpenOrActivateTab(string id, string label, RenderFragment content) => openOrActivate(id, label, content);

    // Ferme un onglet (dynamique ou de menu) — bascule sur son voisin s'il
    // était actif, même logique que la croix de TabBar. Si l'onglet contient
    // une saisie modifiée, demande d'abord « Enregistrer / Annuler les
    // modifications » ; `force` ferme sans demander (ex. juste après un
    // enregistrement réussi).
    public Task CloseTabAsync(string id, bool force = false) => close(id, force);
}
