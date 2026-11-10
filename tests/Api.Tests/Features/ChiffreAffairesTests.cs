using Api.Features.Performance;
using Api.Features.Statistiques;

namespace Api.Tests.Features;

public class ChiffreAffairesTests
{
    private static DateTime D(string iso) => DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Passe_au_mois_au_dela_de_62_jours()
    {
        Assert.Equal("jour", ChiffreAffaires.GranulariteDe(D("2026-01-01"), D("2026-03-03")));
        Assert.Equal("mois", ChiffreAffaires.GranulariteDe(D("2026-01-01"), D("2026-03-04")));
    }

    [Fact]
    public void Liste_les_mois_de_la_periode_meme_partiels()
    {
        Assert.Equal(12, ChiffreAffaires.Intervalles(D("2026-01-15"), D("2026-12-31"), "mois").Count);
        Assert.Equal(["2026-01", "2026-02", "2026-03"], ChiffreAffaires.Intervalles(D("2026-01-31"), D("2026-03-01"), "mois"));
    }

    [Fact]
    public void Liste_les_jours_bornes_incluses()
    {
        Assert.Equal(["2026-02-27", "2026-02-28", "2026-03-01", "2026-03-02"], ChiffreAffaires.Intervalles(D("2026-02-27"), D("2026-03-02"), "jour"));
    }

    [Fact]
    public void Ramene_le_29_fevrier_au_28()
    {
        Assert.Equal(D("2025-02-28"), ChiffreAffaires.DecalerAnnee(D("2024-02-29"), 1));
        Assert.Equal(D("2025-09-29"), ChiffreAffaires.DecalerAnnee(D("2026-09-29"), -1));
    }

    [Fact]
    public void Additionne_par_mois_et_range_N_1_dans_le_mois_correspondant()
    {
        (DateTime?, double)[] lignes = [(D("2025-03-02"), 100), (D("2025-03-20"), 50), (null, 10)];
        Assert.Equal(new Dictionary<string, double> { ["2026-03"] = 150 }, ChiffreAffaires.CumulerParIntervalle(lignes, "mois", 1));
        Assert.Equal("2026-03-20", ChiffreAffaires.CleIntervalle(D("2026-03-20"), "jour"));
    }

    [Theory]
    [InlineData(1.005, 1.0)] // 1.005 vaut en réalité 1.00499999999999989…
    [InlineData(2.5, 2.5)]
    [InlineData(10.125, 10.13)]
    [InlineData(-10.125, -10.12)] // demie arrondie vers le haut
    public void Arrondit_au_centime_comme_Math_round(double value, double expected)
    {
        Assert.Equal(expected, ChiffreAffaires.ArrondiCentimes(value));
    }

    [Fact]
    public void Le_jeu_de_donnees_de_transfert_est_stable()
    {
        var lignes = PerformanceService.GenererLignes(1024);
        Assert.Equal(
            """{"id":1,"societe":"Euro Cargo SA","ville":"Mons","statut":"Planifié","numero":"C-2023-47072","montant":883.84,"date":"2020-09-17T00:00:00.000Z","archive":false}""",
            lignes[0]);
        Assert.Equal(
            """{"id":2,"societe":"Transports Dubois BV","ville":"Luxembourg","statut":"Planifié","numero":"C-2020-66784","montant":5728,"date":"2023-01-05T00:00:00.000Z","archive":false}""",
            lignes[1]);
    }
}
