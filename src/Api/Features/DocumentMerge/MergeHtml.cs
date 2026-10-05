using System.Collections;
using System.Globalization;
using System.Reflection;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Api.Common;

namespace Api.Features.DocumentMerge;

// Moteur de fusion : résout, dans le HTML d'un modèle de document, les jetons
// de fusion ([data-merge-field]) et les blocs répétables ([data-repeat-list])
// posés par l'éditeur du front (frontend/src/features/documents/) contre un
// contexte de données. Logique pure, sans base de données — équivalent de
// merge-html.ts (cheerio) côté NestJS.
public static class MergeHtml
{
    private sealed record Scope(string Prefix, object? Value);

    private static readonly NumberFormatInfo FrBeNumbers = new()
    {
        NumberDecimalSeparator = ",",
        // Espace fine insécable, comme toLocaleString('fr-BE').
        NumberGroupSeparator = " ",
        NumberGroupSizes = [3],
    };

    public static string Resolve(string templateHtml, object? context)
    {
        var document = new HtmlParser().ParseDocument($"<div id=\"dte-merge-root\">{templateHtml}</div>");
        var root = document.GetElementById("dte-merge-root")!;
        List<Scope> rootScope = [new Scope("", context)];

        ExpandRepeats(document, root, rootScope);
        ResolveChips(document, root, rootScope);

        // Chrome de l'éditeur, inutile à l'impression.
        foreach (var element in root.QuerySelectorAll("[contenteditable]")) element.RemoveAttribute("contenteditable");
        foreach (var element in root.QuerySelectorAll("[data-chip-remove]")) element.Remove();
        foreach (var element in root.QuerySelectorAll("[data-repeat-list]"))
        {
            element.RemoveAttribute("data-repeat-list");
            element.RemoveAttribute("data-repeat-label");
        }
        return root.InnerHtml;
    }

    // Valeur désignée par un chemin pointé (« Contrat.Societe.Nom_societe »).
    // Une liste rencontrée en chemin est traversée : le résultat est alors la
    // liste des valeurs de chaque élément. « [] » en fin de segment est ignoré.
    private static object? GetNested(object? value, string dottedPath)
    {
        if (dottedPath.Length == 0) return value;
        if (value is IEnumerable enumerable and not string and not IDictionary)
        {
            var results = new List<object?>();
            foreach (var item in enumerable)
            {
                var result = GetNested(item, dottedPath);
                if (result is IEnumerable nested and not string and not IDictionary) results.AddRange(nested.Cast<object?>());
                else results.Add(result);
            }
            return results;
        }
        if (value is null) return null;

        var separator = dottedPath.IndexOf('.');
        var rawKey = separator < 0 ? dottedPath : dottedPath[..separator];
        var rest = separator < 0 ? "" : dottedPath[(separator + 1)..];
        var key = rawKey.EndsWith("[]", StringComparison.Ordinal) ? rawKey[..^2] : rawKey;
        var next = ReadMember(value, key);
        return rest.Length > 0 ? GetNested(next, rest) : next;
    }

    private static object? ReadMember(object value, string key)
    {
        if (value is IDictionary<string, object?> dictionary) return dictionary.TryGetValue(key, out var member) ? member : null;
        if (value is string or DateTime or decimal || value.GetType().IsPrimitive) return null;
        return value.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
    }

    // Le chemin est résolu dans la portée la plus proche dont le préfixe
    // correspond (élément courant d'un bloc répétable), sinon à la racine.
    private static object? ResolvePath(List<Scope> scopes, string path)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var scope = scopes[i];
            if (path.StartsWith(scope.Prefix, StringComparison.Ordinal)) return GetNested(scope.Value, path[scope.Prefix.Length..]);
        }
        return null;
    }

    public static string FormatValue(object? raw, string? kind)
    {
        var text = ToText(raw);
        if (kind == "date")
        {
            if (text.Length == 0) return text;
            DateTime utc;
            if (raw is DateTime date) utc = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            else if (!JsDate.TryParse(text, out utc)) return text;
            // toLocaleDateString('fr-BE') : jour dans le fuseau du serveur.
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
        if (kind == "number")
        {
            if (text.Length == 0) return text;
            decimal number;
            if (raw is decimal value)
            {
                number = value;
            }
            else
            {
                var parsed = raw switch { double d => d, float f => f, int or long or short or byte => Convert.ToDouble(raw, CultureInfo.InvariantCulture), _ => JsNumber.Parse(text) };
                if (double.IsNaN(parsed)) return text;
                if (!decimal.TryParse(parsed.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return text;
            }
            return Math.Round(number, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", FrBeNumbers);
        }
        return text;
    }

    // Texte d'une valeur ; un objet ou une liste ne s'imprime pas.
    private static string ToText(object? raw) => raw switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        decimal number => JsonConfig.FormatDecimal(number),
        double number => JsNumber.ToString(number),
        float number => JsNumber.ToString(number),
        int or long or short or byte => Convert.ToString(raw, CultureInfo.InvariantCulture)!,
        DateTime date => JsonConfig.FormatDate(date),
        TimeSpan time => JsonConfig.FormatTime(time),
        _ => "",
    };

    private static void ResolveChips(IDocument document, IElement root, List<Scope> scopes)
    {
        foreach (var element in root.QuerySelectorAll("[data-merge-field]").ToList())
        {
            var path = element.GetAttribute("data-merge-field") ?? "";
            var kind = element.GetAttribute("data-kind");
            var raw = ResolvePath(scopes, path);

            if (kind == "image")
            {
                var source = raw as string ?? "";
                if (source.Length > 0)
                {
                    var image = document.CreateElement("img");
                    image.SetAttribute("class", "dte-merged-image");
                    image.SetAttribute("src", source);
                    element.Replace(image);
                }
                else
                {
                    element.Remove();
                }
                continue;
            }

            var span = document.CreateElement("span");
            span.SetAttribute("class", "dte-merged");
            span.TextContent = FormatValue(raw, kind);
            element.Replace(span);
        }
    }

    // Développe les blocs répétables de premier niveau : la ligne modèle
    // (.dte-repeat-row) est copiée une fois par élément de la liste, chaque
    // copie étant résolue avec cet élément comme portée (blocs imbriqués
    // compris).
    private static void ExpandRepeats(IDocument document, IElement root, List<Scope> scopes)
    {
        var topLevelBlocks = root.QuerySelectorAll("[data-repeat-list]")
            .Where(block => !Ancestors(block).Any(ancestor => ancestor.HasAttribute("data-repeat-list")))
            .ToList();

        foreach (var block in topLevelBlocks)
        {
            var listPath = block.GetAttribute("data-repeat-list") ?? "";
            var templateRow = block.QuerySelector(".dte-repeat-row");
            foreach (var tag in block.QuerySelectorAll(".dte-repeat-tag").ToList()) tag.Remove();
            if (templateRow is null) continue;

            var items = ResolvePath(scopes, listPath) is IEnumerable list and not string and not IDictionary ? list.Cast<object?>().ToList() : [];
            var clones = items.Select(item =>
            {
                var clone = (IElement)templateRow.Clone(true);
                List<Scope> itemScopes = [.. scopes, new Scope($"{listPath}.", item)];
                ExpandRepeats(document, clone, itemScopes);
                ResolveChips(document, clone, itemScopes);
                return (INode)clone;
            }).ToArray();

            if (clones.Length > 0) templateRow.After(clones);
            templateRow.Remove();
        }
    }

    private static IEnumerable<IElement> Ancestors(IElement element)
    {
        for (var parent = element.ParentElement; parent is not null; parent = parent.ParentElement) yield return parent;
    }
}
