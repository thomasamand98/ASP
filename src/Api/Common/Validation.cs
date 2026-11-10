using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Common;

public static partial class Zod
{
    private sealed record Issue(string Path, string Message);

    private sealed record FieldInfo(PropertyInfo Property, FieldAttribute Attribute, Type ValueType);

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

    public static T Parse<T>(JsonElement? body) where T : class, new() => (T)Parse(typeof(T), body);

    public static object Parse(Type type, JsonElement? body)
    {
        var issues = new List<Issue>();
        var result = ParseObject(type, body, "", issues, out _);
        if (issues.Count > 0) throw new BadRequestException(string.Join(' ', issues.Select(Describe)));
        return result!;
    }

    private static string Describe(Issue issue) =>
        issue.Path.Length == 0 ? "Corps de requête invalide." : $"« {issue.Path} » : {issue.Message}.";

    private static FieldInfo[] FieldsOf(Type type) => Fields.GetOrAdd(type, static t =>
    {
        // base d'abord puis ordre de déclaration (= ordre des messages)
        var hierarchy = new List<Type>();
        for (var current = t; current is not null && current != typeof(object); current = current.BaseType) hierarchy.Insert(0, current);
        return hierarchy
            .SelectMany(level => level.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(p => p.MetadataToken))
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<FieldAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x =>
            {
                var propertyType = x.Property.PropertyType;
                if (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(Opt<>))
                {
                    throw new InvalidOperationException($"{t.Name}.{x.Property.Name} doit être un Opt<T>.");
                }
                return new FieldInfo(x.Property, x.Attribute!, propertyType.GetGenericArguments()[0]);
            })
            .ToArray();
    });

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    private static object? ParseObject(Type type, JsonElement? element, string path, List<Issue> issues, out bool fatal)
    {
        fatal = false;
        if (element is not { ValueKind: JsonValueKind.Object } json)
        {
            issues.Add(new Issue(path, element is null ? "champ obligatoire" : ExpectedObject(element.Value)));
            fatal = true;
            return null;
        }

        var partial = type.GetCustomAttribute<PartialAttribute>() is not null;
        var instance = Activator.CreateInstance(type)!;
        foreach (var field in FieldsOf(type))
        {
            JsonElement? raw = json.TryGetProperty(field.Property.Name, out var found) ? found : null;
            var fieldPath = Join(path, field.Property.Name);
            var required = field.Attribute.Required && !partial;
            var (isSet, value, fieldFatal) = ParseField(field, raw, required, fieldPath, issues);
            fatal |= fieldFatal;
            // refusée mais du bon type : gardée pour Refine
            if (isSet) field.Property.SetValue(instance, Activator.CreateInstance(field.Property.PropertyType, value));
        }

        if (!fatal && instance is IRefinable refinable)
        {
            refinable.Refine((key, message) => issues.Add(new Issue(Join(path, key), message)));
        }
        return instance;
    }

    private static string ExpectedObject(JsonElement element) => element.ValueKind == JsonValueKind.Undefined ? "champ obligatoire" : "objet attendu";

    private static (bool IsSet, object? Value, bool Fatal) ParseField(FieldInfo field, JsonElement? raw, bool required, string path, List<Issue> issues)
    {
        var attribute = field.Attribute;
        // mauvais type : champ vide, pas de Refine
        (bool, object?, bool) Fail(string message)
        {
            issues.Add(new Issue(path, message));
            return (false, null, true);
        }

        (bool, object?, bool) Reject(string message, object value)
        {
            issues.Add(new Issue(path, message));
            return (true, value, false);
        }

        if (attribute is NumAttribute) return ParseNum(raw, path, issues);

        if (raw is null) return required ? Fail("champ obligatoire") : (false, null, false);
        var json = raw.Value;
        if (json.ValueKind == JsonValueKind.Null && attribute.Nullable) return (true, null, false);

        switch (attribute)
        {
            case TextAttribute or IdAttribute or IntegerTextAttribute or DecAttribute or DateAttribute:
            {
                // nombre accepté comme texte
                string text;
                if (json.ValueKind == JsonValueKind.Number) text = JsNumber.ToString(json.GetDouble());
                else if (json.ValueKind == JsonValueKind.String) text = json.GetString()!;
                else return Fail("texte attendu");

                switch (attribute)
                {
                    case IdAttribute when !IdPattern().IsMatch(text):
                        return Reject("identifiant invalide", text);
                    case IntegerTextAttribute when !IntegerPattern().IsMatch(text):
                        return Reject("nombre entier attendu", text);
                    case DecAttribute:
                        if (!DecimalPattern().IsMatch(text)) return Reject("nombre décimal attendu", text);
                        text = ReplaceFirst(text.Trim(), ',', '.');
                        break;
                    case DateAttribute date:
                        if (text.Length > 0 && !JsDate.TryParse(text, out _)) return Reject("date invalide", text);
                        if (DayPattern().IsMatch(text)) text = $"{text}T00:00:00.000Z";
                        if (date.RejectBlank && text.Length == 0) return Reject("champ obligatoire", text);
                        break;
                }
                return (true, text, false);
            }

            case AmountAttribute:
                if (json.ValueKind == JsonValueKind.Number) return (true, json.GetDouble(), false);
                if (json.ValueKind == JsonValueKind.String) return (true, json.GetString(), false);
                return Fail("Invalid input");

            case OneOfAttribute oneOf:
                if (json.ValueKind != JsonValueKind.String) return Fail("type invalide");
                var choice = json.GetString()!;
                return oneOf.Values.Contains(choice)
                    ? (true, choice, false)
                    : Fail($"valeur non autorisée ({string.Join(", ", oneOf.Values)})");

            case PatternAttribute pattern:
                if (json.ValueKind != JsonValueKind.String) return Fail("texte attendu");
                var candidate = json.GetString()!;
                return pattern.Regex.IsMatch(candidate) ? (true, candidate, false) : Reject(pattern.Message, candidate);

            case StrictIntAttribute strictInt:
            {
                if (json.ValueKind != JsonValueKind.Number) return Fail("nombre attendu");
                var number = json.GetDouble();
                if (!JsNumber.IsInteger(number)) issues.Add(new Issue(path, "type invalide"));
                if (number < strictInt.Min) issues.Add(new Issue(path, $"Number must be greater than or equal to {strictInt.Min}"));
                if (number > strictInt.Max) issues.Add(new Issue(path, $"Number must be less than or equal to {strictInt.Max}"));
                return (true, (int)Math.Clamp(number, int.MinValue, int.MaxValue), false);
            }

            case NestedAttribute:
            {
                if (!typeof(IList).IsAssignableFrom(field.ValueType))
                {
                    var nested = ParseObject(field.ValueType, json, path, issues, out var nestedFatal);
                    return (nested is not null, nested, nestedFatal);
                }
                if (json.ValueKind != JsonValueKind.Array) return Fail("liste attendue");
                var itemType = field.ValueType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(field.ValueType)!;
                var index = 0;
                var listFatal = false;
                foreach (var item in json.EnumerateArray())
                {
                    var parsed = ParseObject(itemType, item, Join(path, (index++).ToString()), issues, out var itemFatal);
                    listFatal |= itemFatal;
                    if (parsed is not null) list.Add(parsed);
                }
                return (true, list, listFatal);
            }

            default:
                throw new InvalidOperationException($"Type de champ non géré : {attribute.GetType().Name}");
        }
    }

    private static (bool IsSet, object? Value, bool Fatal) ParseNum(JsonElement? raw, string path, List<Issue> issues)
    {
        if (raw is null) return (false, null, false);
        var json = raw.Value;
        double number;
        switch (json.ValueKind)
        {
            case JsonValueKind.True:
                return (true, 1d, false);
            case JsonValueKind.False:
                return (true, 0d, false);
            case JsonValueKind.Number:
                number = json.GetDouble();
                break;
            case JsonValueKind.String:
                var trimmed = json.GetString()!.Trim();
                if (trimmed.Length == 0) return (false, null, false);
                number = JsNumber.Parse(ReplaceFirst(trimmed, ',', '.'));
                break;
            default:
                issues.Add(new Issue(path, "nombre attendu"));
                return (false, null, true);
        }
        if (double.IsFinite(number)) return (true, number, false);
        issues.Add(new Issue(path, "nombre attendu"));
        return (false, null, double.IsNaN(number));
    }

    private static string ReplaceFirst(string text, char search, char replacement)
    {
        var index = text.IndexOf(search);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement.ToString(), text.AsSpan(index + 1));
    }

    [GeneratedRegex(@"^\d*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^-?\d*$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^\s*(-?\d+([.,]\d+)?)?\s*$")]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DayPattern();
}

// usage : [Body] CreateXDto dto
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BodyAttribute : ModelBinderAttribute
{
    public BodyAttribute() : base(typeof(ZodBodyBinder))
    {
        BindingSource = BindingSource.Body;
    }
}

public sealed class ZodBodyBinder : IModelBinder
{
    // 10 Mo, pour les logos en base64
    public const long MaxJsonBodySize = 10 * 1024 * 1024;

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var body = await ReadJsonBodyAsync(bindingContext.HttpContext.Request);
        bindingContext.Result = ModelBindingResult.Success(Zod.Parse(bindingContext.ModelType, body));
    }

    // null = pas de corps JSON
    public static async Task<JsonElement?> ReadJsonBodyAsync(HttpRequest request)
    {
        if (request.ContentLength > MaxJsonBodySize) throw new JsonBodyTooLargeException();
        if (!request.HasJsonContentType()) return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxJsonBodySize) throw new JsonBodyTooLargeException();
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) return null;
        try
        {
            using var document = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BadRequestException("Corps de requête JSON invalide.");
        }
    }
}

// pas une HttpException : 500 générique voulu
public sealed class JsonBodyTooLargeException() : Exception("request entity too large");
