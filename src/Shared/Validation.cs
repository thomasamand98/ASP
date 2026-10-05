using System.Text.RegularExpressions;

namespace Horizon.Shared;

// Validation des corps de requête (POST/PATCH) — équivalent des schémas zod
// et du ZodBodyPipe du backend NestJS. Chaque DTO déclare ses champs avec un
// attribut de type ([Text], [Id], [Num]...) et les reçoit dans un Opt<T> :
// « non envoyé » reste ainsi distinct de « vide » ou de null, comme
// undefined en JavaScript. Un corps mal formé est refusé en 400 avec les
// mêmes messages que le backend d'origine ; les champs inconnus sont ignorés.
//
// Dans un contrôleur de l'API : `[Body] CreateChauffeurDto dto` (voir
// Api/Common/Validation.cs, qui lit et valide le corps). Côté front, les champs
// non renseignés (Opt non affecté) ne sont pas envoyés (voir JsonConfig).

// Valeur éventuellement absente du corps de la requête.
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

    // Affecte la valeur seulement si le champ a été envoyé (Prisma ignore de
    // même les champs undefined).
    public void Apply(Action<T> assign)
    {
        if (IsSet) assign(_value);
    }

    object? IOpt.BoxedValue => _value;

    public static implicit operator Opt<T>(T value) => new(value);
}

// Accès non générique à un Opt<T> (sérialisation JSON, voir JsonConfig).
public interface IOpt
{
    bool IsSet { get; }
    object? BoxedValue { get; }
}

// ---------- Types de champs ----------
// Tolérants en entrée (le front envoie parfois un nombre là où un texte est
// attendu, ou l'inverse), stricts en sortie.

[AttributeUsage(AttributeTargets.Property)]
public abstract class FieldAttribute : Attribute
{
    // Champ obligatoire (zod : sans .optional()). Ignoré pour un DTO [Partial].
    public bool Required { get; set; }
    // null accepté (zod : .nullable()).
    public bool Nullable { get; set; }
}

// Texte libre.
public sealed class TextAttribute : FieldAttribute;

// Identifiant (BigInt côté base) : chiffres uniquement ; "" = aucun.
public sealed class IdAttribute : FieldAttribute;

// Entier signé stocké en texte (ex. couleur WinDev) ; "" = aucun.
public sealed class IntegerTextAttribute : FieldAttribute;

// Nombre, ou code 0/1 (un booléen est converti). "" = champ non envoyé.
// Toujours optionnel.
public sealed class NumAttribute : FieldAttribute;

// Décimal transmis en texte (ex. taux de TVA) : « 21,5 » devient « 21.5 » ;
// "" = aucun.
public sealed class DecAttribute : FieldAttribute;

// Montant saisi (nombre ou texte « 12,50 »), converti par le service :
// Opt<object> contenant un double ou une string.
public sealed class AmountAttribute : FieldAttribute;

// Date ou date-heure ISO ; "" = aucune. Une date seule (AAAA-MM-JJ) est
// complétée en minuit UTC.
public sealed class DateAttribute : FieldAttribute
{
    // Refuse "" avec « champ obligatoire ».
    public bool RejectBlank { get; set; }
}

// Valeur parmi une liste fermée (zod : z.enum).
public sealed class OneOfAttribute(params string[] values) : FieldAttribute
{
    public string[] Values { get; } = values;
}

// Texte strict (aucune conversion) respectant un motif.
public sealed class PatternAttribute(string pattern, string message) : FieldAttribute
{
    public Regex Regex { get; } = new(pattern, RegexOptions.CultureInvariant);
    public string Message { get; } = message;
}

// Entier strict (aucune conversion) borné.
public sealed class StrictIntAttribute(int min, int max) : FieldAttribute
{
    public int Min { get; } = min;
    public int Max { get; } = max;
}

// Objet imbriqué (autre DTO) ou liste d'objets (List<Dto>).
public sealed class NestedAttribute : FieldAttribute;

// Tous les champs deviennent optionnels (zod : .partial()).
[AttributeUsage(AttributeTargets.Class)]
public sealed class PartialAttribute : Attribute;

// Règle portant sur plusieurs champs (zod : .refine()), évaluée quand tous
// les champs ont le bon type.
public interface IRefinable
{
    void Refine(Action<string, string> addIssue);
}
