using Horizon.Shared.Models;

namespace Horizon.Web.Common;

// mêmes clés que la table adresses
public sealed record AdresseValues(string Adresse1, string Adresse2, string Adresse3, string CP, string Localite, string Pays, string Pays_full_name)
{
    public static readonly AdresseValues Empty = new("", "", "", "", "", "", "");

    public static AdresseValues From(AdresseView? a) =>
        new(a?.Adresse1 ?? "", a?.Adresse2 ?? "", a?.Adresse3 ?? "", a?.CP ?? "", a?.Localite ?? "", a?.Pays ?? "", a?.Pays_full_name ?? "");

    // ex. "Rue..., 2030 ANTWERPEN, BELGIQUE"
    public static string Format(string? adresse1, string? cp, string? localite, string? paysFullName)
    {
        var ville = string.Join(' ', new[] { cp, localite }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.Join(", ", new[] { adresse1, ville, paysFullName }.Where(v => !string.IsNullOrWhiteSpace(v)));
    }
}
