using Horizon.Shared.Models;
using Horizon.Web.Services;

namespace Horizon.Web.Features.Contrats;

// Listes de la fiche contrat (sélecteurs Société et Type facture, modèles
// « CONTRAT » du bouton « Voir le PDF ») : chargées par la page (Contrats, ou
// onglet Contrats de la fiche Société) plutôt qu'à chaque ouverture de fiche.
// Un échec laisse la liste vide (le sélecteur ne propose alors que « — »).
public sealed class ContratLookups(ApiClient api)
{
    public ApiList<Societe> Societes { get; } = new(api, "societes");
    public List<TypeFacture> TypesFacture { get; private set; } = [];
    public List<DocumentTemplateSummary> Templates { get; private set; } = [];

    public event Action? Changed;

    public async Task LoadAsync()
    {
        Societes.Changed += () => Changed?.Invoke();
        await Task.WhenAll(Societes.RefetchAsync(), LoadTypesFactureAsync(), LoadTemplatesAsync());
    }

    private async Task LoadTypesFactureAsync()
    {
        try
        {
            TypesFacture = (await api.GetAsync<TypesFactureResponse>("types-facture")).typesFacture;
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // Sélecteur limité à « — ».
        }
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            Templates = (await api.GetAsync<DocumentTemplatesResponse>("document-templates?type=CONTRAT")).documentTemplates;
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // Bouton « Voir le PDF » désactivé, comme sans modèle.
        }
    }
}
