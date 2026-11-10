using Bunit;
using Horizon.Web.Components.Table;
using Horizon.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Horizon.Web.Tests.Components;

public class DataTableTests : BunitContext
{
    private sealed record Ligne(string Id, string Nom, double Poids);

    private static readonly Ligne[] Lignes = [new("1", "Béton", 30), new("2", "argile", 5), new("3", "Sable", 12)];

    private static readonly IReadOnlyList<Column<Ligne>> Colonnes =
    [
        Col.Key<Ligne>("Nom", "Nom", l => l.Nom),
        Col.Key<Ligne>("Poids", "Poids", l => l.Poids),
    ];

    public DataTableTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Browser>();
    }

    private HashSet<string> _selection = [];

    private IRenderedComponent<DataTable<Ligne>> Rendre(bool selection = false) => Render<DataTable<Ligne>>(p =>
    {
        p.Add(t => t.Data, Lignes);
        p.Add(t => t.Columns, Colonnes);
        p.Add(t => t.GetRowId, l => l.Id);
        if (selection)
        {
            p.Add(t => t.SelectedRowIds, _selection);
            p.Add(t => t.OnSelectionChange, ids => _selection = ids);
        }
    });

    private static List<string> Noms(IRenderedComponent<DataTable<Ligne>> table) =>
        [.. table.FindAll("tbody tr td:first-child").Select(td => td.TextContent)];

    [Fact]
    public void Affiche_les_lignes_dans_l_ordre_des_donnees() =>
        Assert.Equal(["Béton", "argile", "Sable"], Noms(Rendre()));

    [Fact]
    public void Trie_le_texte_puis_inverse_au_second_clic()
    {
        var table = Rendre();
        table.Find("th .data-table-sort-label").Click();
        // tri texte : minuscules, code par code
        Assert.Equal(["argile", "Béton", "Sable"], Noms(table));
        Assert.Contains("▲", table.Find("th .data-table-sort-label").TextContent);
        table.Find("th .data-table-sort-label").Click();
        Assert.Equal(["Sable", "Béton", "argile"], Noms(table));
    }

    [Fact]
    public void Trie_les_nombres_du_plus_grand_au_plus_petit_au_premier_clic()
    {
        var table = Rendre();
        table.FindAll("th .data-table-sort-label")[1].Click();
        Assert.Equal(["Béton", "Sable", "argile"], Noms(table));
    }

    [Fact]
    public void Filtre_une_colonne_sans_tenir_compte_de_la_casse()
    {
        var table = Rendre();
        table.Find("th .data-table-filter-button").Click();
        table.Find(".data-table-filter-input").Input("SA");
        Assert.Equal(["Sable"], Noms(table));
        table.Find(".data-table-filter-input").Input("zzz");
        Assert.Equal("Aucun résultat", table.Find(".data-table-empty-row").TextContent);
    }

    [Fact]
    public void Clic_selectionne_une_ligne_Ctrl_ajoute_Maj_etend()
    {
        var table = Rendre(selection: true);
        table.FindAll("tbody tr")[0].Click();
        Assert.Equal(["1"], _selection);

        table.Render(p => p.Add(t => t.SelectedRowIds, _selection));
        table.FindAll("tbody tr")[2].Click(new MouseEventArgs { CtrlKey = true });
        Assert.Equal(["1", "3"], _selection.Order());

        table.Render(p => p.Add(t => t.SelectedRowIds, _selection));
        // ancre sur 3 après le Ctrl+clic, Maj+clic remplace par 2-3
        table.FindAll("tbody tr")[1].Click(new MouseEventArgs { ShiftKey = true });
        Assert.Equal(["2", "3"], _selection.Order());
    }

    [Fact]
    public void La_case_d_en_tete_selectionne_les_lignes_visibles()
    {
        var table = Rendre(selection: true);
        table.Find("th .data-table-filter-button").Click();
        table.Find(".data-table-filter-input").Input("a");
        table.Find("thead input[type=checkbox]").Change(true);
        // pas de "a" dans Béton : lignes affichées seulement
        Assert.Equal(["2", "3"], _selection.Order());
    }
}
