using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Horizon.Shared;

namespace Horizon.Web.Common;

// Passage d'un modèle de formulaire (classe simple, mêmes noms de champs que
// le DTO de l'API) au DTO partagé (Shared/Dtos) envoyé à l'API. Comme les
// formulaires du front d'origine, qui envoyaient leur objet `form` entier :
// chaque champ du modèle est envoyé ; les champs du DTO absents du modèle
// ne le sont pas.
public static class FormModel
{
    private static readonly ConcurrentDictionary<(Type, Type), (PropertyInfo Source, PropertyInfo Target)[]> Maps = new();

    public static TDto ToDto<TDto>(object model) where TDto : new()
    {
        var dto = new TDto();
        foreach (var (source, target) in Maps.GetOrAdd((model.GetType(), typeof(TDto)), static key => Map(key.Item1, key.Item2)))
        {
            target.SetValue(dto, Activator.CreateInstance(target.PropertyType, Convert(source.GetValue(model), target.PropertyType.GetGenericArguments()[0])));
        }
        return dto;
    }

    private static (PropertyInfo, PropertyInfo)[] Map(Type model, Type dto) =>
        [.. dto.GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Opt<>))
            .Select(target => (Source: model.GetProperty(target.Name), Target: target))
            // Champs simples seulement : les listes et objets imbriqués
            // (plages horaires d'un point...) sont convertis par le formulaire.
            .Where(pair => pair.Source is not null && IsScalar(pair.Source.PropertyType) && IsScalar(pair.Target.PropertyType.GetGenericArguments()[0]))
            .Select(pair => (pair.Source!, pair.Target))];

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(object);
    }

    private static object? Convert(object? value, Type type)
    {
        if (value is null || type == typeof(object)) return value;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsInstanceOfType(value) ? value : System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }

    // Valeurs préremplies en création (projection depuis une seule ligne) :
    // remplacent les champs texte de même nom du modèle (comme
    // `{ ...form, ...defaults }`).
    public static void ApplyDefaults(object model, IReadOnlyDictionary<string, string>? defaults)
    {
        if (defaults is null) return;
        foreach (var (name, value) in defaults)
        {
            var property = model.GetType().GetProperty(name);
            if (property?.PropertyType == typeof(string) && property.CanWrite) property.SetValue(model, value);
        }
    }

    // Un id 0 (défaut WinDev) équivaut à « aucun lien ».
    public static string LinkId(string? value) => value is { Length: > 0 } and not "0" ? value : "";

    // Date ISO renvoyée par l'API → « AAAA-MM-JJ » d'un <input type="date">.
    public static string DateInput(string? value) => string.IsNullOrEmpty(value) ? "" : value[..Math.Min(10, value.Length)];

    // Tri « fr » des listes de choix (localeCompare(..., 'fr')).
    public static readonly StringComparer French = StringComparer.Create(new CultureInfo("fr"), CompareOptions.None);

    // [a, b].filter(Boolean).join(' ').
    public static string JoinNonEmpty(string separator, params string?[] parts) => string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
}
