using Horizon.Shared.Models;

namespace Horizon.Web.Common;

// Adresse aplatie, mêmes clés que la table `adresses` (voir les DTO Contact,
// Point, Personnel et Entité, que l'API enregistre à part). Saisie avec
// Components/AdresseFields.razor.
public sealed record AdresseValues(string Adresse1, string Adresse2, string Adresse3, string CP, string Localite, string Pays, string Pays_full_name)
{
    public static readonly AdresseValues Empty = new("", "", "", "", "", "", "");

    // Valeurs du formulaire depuis l'adresse renvoyée par l'API (null → vide).
    public static AdresseValues From(AdresseView? a) =>
        new(a?.Adresse1 ?? "", a?.Adresse2 ?? "", a?.Adresse3 ?? "", a?.CP ?? "", a?.Localite ?? "", a?.Pays ?? "", a?.Pays_full_name ?? "");

    // Une ligne lisible : « Rue…, 2030 ANTWERPEN, BELGIQUE ».
    public static string Format(string? adresse1, string? cp, string? localite, string? paysFullName)
    {
        var ville = string.Join(' ', new[] { cp, localite }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.Join(", ", new[] { adresse1, ville, paysFullName }.Where(v => !string.IsNullOrWhiteSpace(v)));
    }
}
