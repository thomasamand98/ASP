using Horizon.Shared.Models;
using Horizon.Web.Services;

namespace Horizon.Web.Features.Contrats;

// chargées par la page plutôt qu'à chaque fiche, vides en cas d'échec
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
            // sélecteur limité à "—"
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
            // Voir le PDF désactivé, comme sans modèle
        }
    }
}
