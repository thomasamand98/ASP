using Api.Features.DocumentMerge;

namespace Api.Tests.Features;

// Portage de backend/src/document-merge/merge-html.spec.ts.
public class MergeHtmlTests
{
    private static string Chip(string path, string? kind = null) =>
        $"<span data-merge-field=\"{path}\"{(kind is null ? "" : $" data-kind=\"{kind}\"")} contenteditable=\"false\">{path}<button data-chip-remove>x</button></span>";

    [Fact]
    public void Remplace_les_jetons_par_leur_valeur_formatee_et_retire_le_chrome_de_l_editeur()
    {
        var html = MergeHtml.Resolve(
            $"<p>{Chip("Contrat.Num_contrat")} du {Chip("Contrat.Date", "date")} : {Chip("Contrat.Total", "number")}</p>",
            new { Contrat = new { Num_contrat = "C-42", Date = new DateTime(2026, 9, 25), Total = 1234.5m } });
        Assert.Contains("C-42", html);
        Assert.Contains("25/09/2026", html);
        // Séparateur de milliers : espace fine insécable, comme fr-BE côté Node.
        Assert.Contains("1 234,50", html);
        Assert.DoesNotContain("data-merge-field", html);
        Assert.DoesNotContain("contenteditable", html);
        Assert.DoesNotContain("<button", html);
    }

    [Fact]
    public void Echappe_le_texte_des_valeurs()
    {
        var html = MergeHtml.Resolve($"<p>{Chip("Societe.Nom")}</p>", new { Societe = new { Nom = "<script>alert(1)</script>" } });
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void N_ecrit_rien_pour_un_chemin_qui_pointe_vers_un_objet_ou_qui_n_existe_pas()
    {
        var html = MergeHtml.Resolve(
            $"<p>{Chip("Contrat.Societe")}|{Chip("Contrat.Absent")}|{Chip("Contrat.Societe", "number")}</p>",
            new { Contrat = new { Societe = new { Nom = "ACME" } } });
        Assert.Equal("<p><span class=\"dte-merged\"></span>|<span class=\"dte-merged\"></span>|<span class=\"dte-merged\"></span></p>", html);
    }

    [Fact]
    public void Developpe_un_bloc_repetable_une_fois_par_element()
    {
        var template = $"<table data-repeat-list=\"Contrat.Prestations[]\"><tbody><tr class=\"dte-repeat-row\"><td>{Chip("Contrat.Prestations[].Nom")}</td></tr></tbody></table>";
        var html = MergeHtml.Resolve(template, new { Contrat = new { Prestations = new[] { new { Nom = "Transport" }, new { Nom = "Attente" } } } });
        Assert.Equal(2, html.Split("dte-repeat-row").Length - 1);
        Assert.True(html.IndexOf("Transport", StringComparison.Ordinal) < html.IndexOf("Attente", StringComparison.Ordinal));
        Assert.DoesNotContain("data-repeat-list", html);
    }

    [Fact]
    public void Developpe_les_blocs_imbriques_avec_l_element_courant_comme_portee()
    {
        var template =
            "<table data-repeat-list=\"Contrat.Prestations[]\"><tbody><tr class=\"dte-repeat-row\"><td>"
            + Chip("Contrat.Prestations[].Nom")
            + "<table data-repeat-list=\"Contrat.Prestations[].Supplements[]\"><tbody><tr class=\"dte-repeat-row\"><td>"
            + Chip("Contrat.Prestations[].Supplements[].Libelle")
            + "</td></tr></tbody></table></td></tr></tbody></table>";
        var context = new
        {
            Contrat = new
            {
                Prestations = new[]
                {
                    new { Nom = "Transport", Supplements = new[] { new { Libelle = "Attente" }, new { Libelle = "Péage" } } },
                    new { Nom = "Manutention", Supplements = Array.Empty<object>().Select(_ => new { Libelle = "" }).ToArray() },
                },
            },
        };
        var html = MergeHtml.Resolve(template, context);
        Assert.Contains("Transport", html);
        Assert.Contains("Attente", html);
        Assert.Contains("Péage", html);
        Assert.Contains("Manutention", html);
        Assert.Equal(1, html.Split("Attente").Length - 1);
    }

    [Fact]
    public void Remplace_un_jeton_image_par_une_img_ou_le_retire_sans_valeur()
    {
        Assert.Equal(
            "<img class=\"dte-merged-image\" src=\"data:image/png;base64,AAAA\">",
            MergeHtml.Resolve(Chip("Entite.Logo", "image"), new { Entite = new { Logo = "data:image/png;base64,AAAA" } }));
        Assert.Equal("", MergeHtml.Resolve(Chip("Entite.Logo", "image"), new { Entite = new { Logo = (string?)null } }));
    }

    [Theory]
    [InlineData(0.005, "0,01")]
    [InlineData(1.005, "1,01")]
    [InlineData(2.675, "2,68")]
    [InlineData(12d, "12,00")]
    [InlineData(-1234567.891, "-1 234 567,89")]
    public void Formate_les_nombres_comme_toLocaleString_fr_BE(double value, string expected)
    {
        Assert.Equal(expected, MergeHtml.FormatValue(value, "number"));
    }
}
