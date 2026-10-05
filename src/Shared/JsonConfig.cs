using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Horizon.Shared;

// Sérialisation JSON identique à celle du backend NestJS/Prisma, dont dépend
// le front :
//   - noms de champs tels quels (IDSOCIETES, Nom_societe, accessToken...) —
//     aucune conversion en camelCase ;
//   - BigInt (long) → chaîne ;
//   - Decimal → chaîne sans zéros inutiles (« 21 », « 21.5 ») ;
//   - dates → ISO 8601 UTC avec millisecondes (« 2026-01-15T00:00:00.000Z »),
//     heures (colonnes TIME) → même format sur le 1er janvier 1970.
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions());

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = null;
        options.DictionaryKeyPolicy = null;
        // Comme JSON.stringify : accents et « < > & » écrits tels quels.
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Add(new BigIntConverter());
        options.Converters.Add(new DecimalConverter());
        options.Converters.Add(new IsoDateTimeConverter());
        options.Converters.Add(new TimeConverter());
        // DTO envoyés par le front : un Opt<T> non affecté est omis (comme un
        // champ undefined en JavaScript), sinon sa valeur est écrite telle quelle.
        options.Converters.Add(new OptConverterFactory());
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(SkipUnsetOpt);
        return options;
    }

    private static void SkipUnsetOpt(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Opt<>))
            {
                property.ShouldSerialize = (_, value) => value is IOpt { IsSet: true };
            }
        }
    }

    private sealed class OptConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Opt<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(OptConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
    }

    private sealed class OptConverter<T> : JsonConverter<Opt<T>>
    {
        public override Opt<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<T>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, Opt<T> value, JsonSerializerOptions options)
        {
            // Opt<object> (montants) : la valeur est écrite selon son type réel.
            var boxed = ((IOpt)value).BoxedValue;
            if (boxed is null) writer.WriteNullValue();
            else JsonSerializer.Serialize(writer, boxed, boxed.GetType(), options);
        }
    }

    public static string FormatDecimal(decimal value) => value.ToString("0.#############################", CultureInfo.InvariantCulture);

    // Date.prototype.toISOString(). Les dates lues en base sont renvoyées
    // telles quelles, sans conversion de fuseau, et présentées comme de
    // l'UTC — comme le fait Prisma (voir AppDbContext.WallClockConverter).
    public static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string FormatTime(TimeSpan value) => FormatDate(DateTime.UnixEpoch + value);

    private sealed class BigIntConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? long.Parse(reader.GetString()!, CultureInfo.InvariantCulture) : reader.GetInt64();

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class DecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture) : reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteStringValue(FormatDecimal(value));
    }

    private sealed class IsoDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(FormatDate(value));
    }

    private sealed class TimeConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            TimeSpan.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteStringValue(FormatTime(value));
    }
}
