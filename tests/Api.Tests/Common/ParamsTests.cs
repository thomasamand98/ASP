using Api.Common;

namespace Api.Tests.Common;

// Portage de backend/src/common/params.spec.ts.
public class ParamsTests
{
    [Fact]
    public void ParseId_convertit_un_identifiant_numerique()
    {
        Assert.Equal(9007199254740993L, Params.ParseId("9007199254740993"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseId_refuse_en_400(string? raw)
    {
        Assert.Equal("Identifiant invalide.", Assert.Throws<BadRequestException>(() => Params.ParseId(raw)).Message);
    }

    [Fact]
    public void ParseOptionalId_renvoie_null_quand_le_parametre_est_absent_ou_vide()
    {
        Assert.Null(Params.ParseOptionalId(null));
        Assert.Null(Params.ParseOptionalId(""));
        Assert.Equal(42L, Params.ParseOptionalId("42"));
        Assert.Throws<BadRequestException>(() => Params.ParseOptionalId("x"));
    }

    [Fact]
    public void La_pagination_reste_desactivee_quand_elle_est_absente()
    {
        Assert.Null(Params.ParsePage(null));
        Assert.Null(Params.ParsePageSize(null));
    }

    [Fact]
    public void PageSize_0_est_garde_et_les_grandes_tailles_sont_plafonnees()
    {
        Assert.Equal(0, Params.ParsePageSize("0"));
        Assert.Equal(25, Params.ParsePageSize("25"));
        Assert.Equal(Params.MaxPageSize, Params.ParsePageSize("10000000"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("1.5")]
    public void ParsePage_refuse(string raw) => Assert.Throws<BadRequestException>(() => Params.ParsePage(raw));

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public void ParsePageSize_refuse(string raw) => Assert.Throws<BadRequestException>(() => Params.ParsePageSize(raw));

    [Fact]
    public void ParsePeriod_lit_les_bornes_et_refuse_une_periode_inversee()
    {
        Assert.Null(Params.ParsePeriod(null, ""));
        var period = Params.ParsePeriod("2026-01-01", null)!;
        Assert.Equal(new DateTime(2026, 1, 1), period.Gte);
        Assert.Null(period.Lte);
        Assert.Throws<BadRequestException>(() => Params.ParsePeriod("2026-12-31", "2026-01-01"));
        Assert.Equal("Date invalide pour « from ».", Assert.Throws<BadRequestException>(() => Params.ParsePeriod("01-01-2026", null)).Message);
    }

    [Fact]
    public void Projection_lit_via_et_ids()
    {
        Assert.Null(Projection.Parse(null, "1,2"));
        var projection = Projection.Parse("societes", "12, 15,")!;
        Assert.Equal("societes", projection.Via);
        Assert.Equal([12L, 15L], projection.Ids);
        Assert.Equal("Paramètre ids invalide", Assert.Throws<BadRequestException>(() => Projection.Parse("societes", "1,x")).Message);
    }

    [Theory]
    [InlineData("12", 12d)]
    [InlineData(" 1.5 ", 1.5)]
    [InlineData("", 0d)]
    [InlineData("1e3", 1000d)]
    [InlineData("0x10", 16d)]
    public void JsNumber_lit_comme_Number_de_JavaScript(string raw, double expected) => Assert.Equal(expected, JsNumber.Parse(raw));

    [Theory]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1,5")]
    public void JsNumber_renvoie_NaN_pour_un_texte_non_numerique(string raw) => Assert.True(double.IsNaN(JsNumber.Parse(raw)));

    [Fact]
    public void JsDate_lit_les_dates_ISO_comme_Date_parse()
    {
        Assert.True(JsDate.TryParse("2026-01-15", out var day));
        Assert.Equal(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), day);
        Assert.True(JsDate.TryParse("2026-01-15T10:30:00+02:00", out var offset));
        Assert.Equal(new DateTime(2026, 1, 15, 8, 30, 0, DateTimeKind.Utc), offset);
        Assert.True(JsDate.TryParse("2026-01-15T23:59:59.123Z", out var utc));
        Assert.Equal(123, utc.Millisecond);
        Assert.False(JsDate.TryParse("2026-13-45", out _));
        Assert.False(JsDate.TryParse("pas une date", out _));
    }
}
