using Horizon.Shared;

namespace Horizon.Web.Tests.Common;

public class WindevColorTests
{
    [Fact]
    public void Convertit_un_entier_BGR_en_hexadecimal()
    {
        // RGB(255, 128, 0) = 255 + 128×256
        Assert.Equal("#ff8000", WindevColor.ToHex(255 + 128 * 256L));
        Assert.Equal("#0000ff", WindevColor.ToHex((object)"16711680"));
    }

    [Fact]
    public void Traite_zero_vide_et_hors_plage_comme_pas_de_couleur()
    {
        Assert.Null(WindevColor.ToHex(0L));
        Assert.Null(WindevColor.ToHex((object)""));
        Assert.Null(WindevColor.ToHex(0x1000000L));
    }

    [Fact]
    public void Fait_l_aller_retour_depuis_un_input_color()
    {
        Assert.Equal("#12abef", WindevColor.ToHex(WindevColor.FromHex("#12abef")));
        Assert.Equal(0, WindevColor.FromHex("invalide"));
    }
}
