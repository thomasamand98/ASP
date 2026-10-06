using System.Net;
using System.Text;
using Bunit;
using Horizon.Web.Components;
using Horizon.Web.Components.Table;
using Horizon.Web.Components.UnsavedChanges;
using Horizon.Web.Layout;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Horizon.Web.Tests.Components;

public class TabBarTests : BunitContext
{
    private static readonly TabInfo[] Onglets = [new("a", "Société"), new("b", "Contrats"), new("c", "Planning")];

    [Fact]
    public void Marque_l_onglet_actif_et_signale_selection_et_fermeture()
    {
        string? selection = null, fermeture = null;
        var bar = Render<TabBar>(p => p
            .Add(t => t.Tabs, Onglets)
            .Add(t => t.ActiveId, "b")
            .Add(t => t.OnSelect, id => selection = id)
            .Add(t => t.OnClose, id => fermeture = id));

        Assert.Equal("Contrats", bar.Find(".tab-bar-tab.active .tab-bar-tab-label").TextContent);
        bar.FindAll(".tab-bar-tab-label")[2].Click();
        Assert.Equal("c", selection);
        bar.FindAll(".tab-bar-tab-close")[0].Click();
        Assert.Equal("a", fermeture);
    }

    [Fact]
    public void Glisser_deposer_reordonne_les_onglets()
    {
        List<string>? ordre = null;
        var bar = Render<TabBar>(p => p
            .Add(t => t.Tabs, Onglets)
            .Add(t => t.ActiveId, "a")
            .Add(t => t.OnReorder, ids => ordre = ids));

        bar.FindAll(".tab-bar-tab")[2].DragStart();
        bar.FindAll(".tab-bar-tab")[0].Drop();
        Assert.Equal(["c", "a", "b"], ordre);
    }
}

public class CrudPageTests : BunitContext
{
    public sealed record Item(string Id, string Nom);

    public sealed class ItemDto
    {
        public string Nom { get; set; } = "";
    }

    // GET /items : liste initiale.
    private sealed class FakeApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"items":[{"Id":"1","Nom":"Alpha"},{"Id":"2","Nom":"Bravo"},{"Id":"3","Nom":"Charlie"}]}""", Encoding.UTF8, "application/json"),
            });
    }

    private readonly List<string> _supprimes = [];
    // Fiche ouverte dans un onglet de l'application (mode de vue par défaut).
    private (string Label, RenderFragment Content)? _ongletOuvert;
    private readonly ApiList<Item> _liste;

    public CrudPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<bool>("horizon.confirm", _ => true).SetResult(true);
        var http = new HttpClient(new FakeApi()) { BaseAddress = new Uri("http://api.test/") };
        var session = new AuthSession(http, new AppConfig());
        Services.AddSingleton<Browser>();
        Services.AddSingleton<UnsavedChangesService>();
        _liste = new ApiList<Item>(new ApiClient(http, session), "items");
    }

    private IRenderedComponent<CrudPage<Item, ItemDto, Item>> Rendre(Func<ItemDto, Task<Item>>? create = null)
    {
        _liste.RefetchAsync().GetAwaiter().GetResult();
        var tabs = new TabsContext((_, label, content) => _ongletOuvert = (label, content), (_, _) => Task.CompletedTask);
        return Render<CrudPage<Item, ItemDto, Item>>(p => p
            .AddCascadingValue(tabs)
            .Add(c => c.Title, "Éléments")
            .Add(c => c.LoadingLabel, "Chargement...")
            .Add(c => c.Source, _liste)
            .Add(c => c.Columns, [Col.Key<Item>("Nom", "Nom", i => i.Nom)])
            .Add(c => c.GetRowId, i => i.Id)
            .Add(c => c.Create, create ?? (dto => Task.FromResult(new Item("4", dto.Nom))))
            .Add(c => c.Update, (id, dto) => Task.FromResult(new Item(id, dto.Nom)))
            .Add(c => c.Remove, id =>
            {
                // « Bravo » est encore référencé ailleurs : l'API refuse.
                if (id == "2") throw new ApiException("Élément utilisé", 409);
                _supprimes.Add(id);
                return Task.CompletedTask;
            })
            .Add(c => c.DeleteConfirmMessage, "Supprimer cet élément ?")
            .Add(c => c.CreateModalTitle, "Nouvel élément")
            .Add(c => c.EditModalTitle, "Modifier l'élément")
            .Add(c => c.RenderForm, form => b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "id", "enregistrer");
                b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => form.OnSubmit(new ItemDto { Nom = "Delta" })));
                b.CloseElement();
            }));
    }

    private static List<string> Noms(IRenderedComponent<CrudPage<Item, ItemDto, Item>> page) =>
        [.. page.FindAll(".data-table tbody tr td:last-child").Select(td => td.TextContent)];

    [Fact]
    public void Affiche_le_titre_avec_le_nombre_de_lignes()
    {
        var page = Rendre();
        Assert.Equal("Éléments (3)", page.Find(".page-header h2").TextContent);
        Assert.Equal(["Alpha", "Bravo", "Charlie"], Noms(page));
    }

    [Fact]
    public void La_creation_ajoute_la_ligne_renvoyee_par_l_API()
    {
        var page = Rendre();
        page.Find(".page-actions .btn.primary").Click();
        Assert.Equal("Nouvel élément", _ongletOuvert?.Label);
        var fiche = Render(_ongletOuvert!.Value.Content);
        fiche.Find("#enregistrer").Click();
        page.WaitForAssertion(() => Assert.Contains("Delta", Noms(page)));
    }

    [Fact]
    public void La_suppression_multiple_garde_les_lignes_en_echec_selectionnees()
    {
        var page = Rendre();
        var lignes = page.FindAll(".data-table tbody tr");
        lignes[0].Click();
        page.FindAll(".data-table tbody tr")[1].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { ShiftKey = true });
        page.FindAll(".data-table tbody tr")[2].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { CtrlKey = true });
        Assert.Contains("Supprimer (3)", page.Find(".page-actions .btn.danger").TextContent);

        page.Find(".page-actions .btn.danger").Click();

        page.WaitForAssertion(() => Assert.Equal(["Bravo"], Noms(page)));
        Assert.Equal(["1", "3"], _supprimes.Order());
        // La ligne en échec reste sélectionnée et l'échec est annoncé.
        Assert.Contains("selected", page.Find(".data-table tbody tr").ClassName);
        var alerte = JSInterop.VerifyInvoke("horizon.alert").Arguments[0] as string;
        Assert.Equal("1 suppression(s) sur 3 ont échoué : Élément utilisé", alerte);
    }
}
