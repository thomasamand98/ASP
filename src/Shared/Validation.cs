using System.Text.RegularExpressions;

namespace Horizon.Shared;

// Opt<T> pour distinguer non envoyé / vide / null, champs inconnus ignorés
public readonly struct Opt<T> : IOpt
{
    private readonly T _value;

    public Opt(T value)
    {
        _value = value;
        IsSet = true;
    }

    public bool IsSet { get; }
    public T Value => IsSet ? _value : throw new InvalidOperationException("Champ non envoyé.");
    public T? OrDefault => IsSet ? _value : default;

    public void Apply(Action<T> assign)
    {
        if (IsSet) assign(_value);
    }

    object? IOpt.BoxedValue => _value;

    public static implicit operator Opt<T>(T value) => new(value);
}

// accès non générique, pour JsonConfig
public interface IOpt
{
    bool IsSet { get; }
    object? BoxedValue { get; }
}

// types de champs : tolérants en entrée (nombre/texte), stricts en sortie

[AttributeUsage(AttributeTargets.Property)]
public abstract class FieldAttribute : Attribute
{
    // ignoré pour un DTO [Partial]
    public bool Required { get; set; }
    public bool Nullable { get; set; }
}

public sealed class TextAttribute : FieldAttribute;

// id BigInt, chiffres seulement, "" = aucun
public sealed class IdAttribute : FieldAttribute;

// entier signé en texte (couleur WinDev), "" = aucun
public sealed class IntegerTextAttribute : FieldAttribute;

// nombre ou 0/1 (booléen converti), "" = non envoyé
public sealed class NumAttribute : FieldAttribute;

// décimal en texte, "21,5" -> "21.5", "" = aucun
public sealed class DecAttribute : FieldAttribute;

// double ou string ("12,50"), converti par le service
public sealed class AmountAttribute : FieldAttribute;

// ISO, date seule = minuit UTC, "" = aucune
public sealed class DateAttribute : FieldAttribute
{
    // "" refusé (champ obligatoire)
    public bool RejectBlank { get; set; }
}

public sealed class OneOfAttribute(params string[] values) : FieldAttribute
{
    public string[] Values { get; } = values;
}

// texte strict, sans conversion
public sealed class PatternAttribute(string pattern, string message) : FieldAttribute
{
    public Regex Regex { get; } = new(pattern, RegexOptions.CultureInvariant);
    public string Message { get; } = message;
}

// entier strict, sans conversion
public sealed class StrictIntAttribute(int min, int max) : FieldAttribute
{
    public int Min { get; } = min;
    public int Max { get; } = max;
}

// DTO imbriqué ou List<Dto>
public sealed class NestedAttribute : FieldAttribute;

// tous les champs optionnels (modif)
[AttributeUsage(AttributeTargets.Class)]
public sealed class PartialAttribute : Attribute;

// règle multi-champs, après vérif des types
public interface IRefinable
{
    void Refine(Action<string, string> addIssue);
}
