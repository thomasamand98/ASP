using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Horizon.Shared;

namespace Horizon.Web.Common;

// copie par nom de propriété, champs absents du modèle laissés non définis
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
            // listes et objets imbriqués : à convertir dans le formulaire
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

    // préremplissage en création, champs texte seulement
    public static void ApplyDefaults(object model, IReadOnlyDictionary<string, string>? defaults)
    {
        if (defaults is null) return;
        foreach (var (name, value) in defaults)
        {
            var property = model.GetType().GetProperty(name);
            if (property?.PropertyType == typeof(string) && property.CanWrite) property.SetValue(model, value);
        }
    }

    // id 0 = pas de lien (défaut WinDev)
    public static string LinkId(string? value) => value is { Length: > 0 } and not "0" ? value : "";

    public static string DateInput(string? value) => string.IsNullOrEmpty(value) ? "" : value[..Math.Min(10, value.Length)];

    public static readonly StringComparer French = StringComparer.Create(new CultureInfo("fr"), CompareOptions.None);

    public static string JoinNonEmpty(string separator, params string?[] parts) => string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
}
