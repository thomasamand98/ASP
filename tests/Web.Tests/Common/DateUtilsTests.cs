using Horizon.Web.Common;

namespace Horizon.Web.Tests.Common;

public class DateUtilsTests
{
    [Fact]
    public void Fait_l_aller_retour_d_un_jour_local()
    {
        var date = new DateTime(2026, 1, 5);
        Assert.Equal("2026-01-05", DateUtils.ToInputValue(date));
        Assert.Equal(date, DateUtils.FromInputValue("2026-01-05"));
    }

    [Fact]
    public void Refuse_une_valeur_incomplete()
    {
        Assert.Null(DateUtils.FromInputValue(""));
        Assert.Null(DateUtils.FromInputValue("2026-01"));
    }

    [Fact]
    public void DaysInRange_inclut_les_deux_bornes()
    {
        Assert.Equal(
            ["2026-09-28", "2026-09-29", "2026-09-30", "2026-10-01"],
            DateUtils.DaysInRange(new DateTime(2026, 9, 28), new DateTime(2026, 10, 1)).Select(DateUtils.ToInputValue));
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(8, 15)]
    [InlineData(52, 45)]
    public void Snap_arrondit_au_quart_d_heure_le_plus_proche(double minutes, double attendu) =>
        Assert.Equal(attendu, DateUtils.Snap(minutes));

    [Fact]
    public void LayoutLanes_met_cote_a_cote_les_elements_qui_se_chevauchent()
    {
        var lanes = DateUtils.LayoutLanes([("a", 0, 60), ("b", 30, 90), ("c", 60, 120), ("d", 200, 260)]);
        Assert.Equal((0, 2), lanes["a"]);
        Assert.Equal((1, 2), lanes["b"]);
        // c reprend le couloir de a, déjà fini
        Assert.Equal((0, 2), lanes["c"]);
        // d seul dans son groupe
        Assert.Equal((0, 1), lanes["d"]);
    }
}
