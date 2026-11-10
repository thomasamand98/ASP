using Horizon.Web.Common;

namespace Horizon.Web.Tests.Common;

// valeurs de référence, graphique à 1 043 px
public class ChartMathTests
{
    [Fact]
    public void NiceTicks_arrondit_l_axe_comme_le_graphique_d_origine()
    {
        Assert.Equal([0m, 150_000m, 300_000m, 450_000m, 600_000m], ChartMath.NiceTicks(0, 433_304.65m));
        Assert.Equal([0m, 45_000m, 90_000m, 135_000m, 180_000m], ChartMath.NiceTicks(0, 170_000m));
    }

    [Fact]
    public void NiceTicks_sans_donnees_donne_quatre_pas_de_un() =>
        Assert.Equal([0m, 1m, 2m, 3m, 4m], ChartMath.NiceTicks(0, 0));

    [Fact]
    public void BarPosition_reprend_largeur_et_ecart_des_barres()
    {
        // 12 mois sur 955 px, 4 séries, 18 % de marge, 2 px entre barres
        var band = 955d / 12;
        var (offset, size) = ChartMath.BarPosition(band, 4, 0, 0.18, 2, 28);
        Assert.Equal(11, size);
        Assert.Equal(14.325, offset, 3);
        Assert.Equal(14.325 + 13, ChartMath.BarPosition(band, 4, 1, 0.18, 2, 28).Offset, 3);
    }

    [Fact]
    public void VisibleXTicks_garde_premiere_et_derniere_et_saute_ce_qui_chevauche()
    {
        // 46 jours : une étiquette sur deux + la dernière
        var band = 955d / 46;
        var centers = Enumerable.Range(0, 46).Select(i => 80 + (i + 0.5) * band).ToList();
        var widths = Enumerable.Repeat(31.453125, 46).ToList();
        int[] attendu = [.. Enumerable.Range(0, 22).Select(i => i * 2), 45];
        Assert.Equal(attendu, ChartMath.VisibleXTicks(centers, widths, 0, 1043, 8));
    }
}
