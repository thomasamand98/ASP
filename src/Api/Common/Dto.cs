using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Common;

// Conversions des valeurs reçues dans les DTO vers les types des colonnes.
public static class Dto
{
    // Identifiant reçu en texte (voir [Id]) → BigInt.
    public static long Id(string value) => long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    // Nombre reçu (voir [Num]) vers une colonne entière. Comme Prisma (vérifié
    // sur le backend d'origine) : la partie décimale est tronquée (2.7 → 2,
    // -1.5 → -1) et un nombre hors des bornes d'un entier 32 bits provoque
    // une erreur 500.
    public static int Int(double value)
    {
        var truncated = Math.Truncate(value);
        return truncated is >= int.MinValue and <= int.MaxValue
            ? (int)truncated
            : throw new InvalidOperationException($"Entier 32 bits attendu, reçu {value.ToString(CultureInfo.InvariantCulture)}.");
    }

    // Identifiant de clé étrangère reçu en texte : absent = champ inchangé,
    // "" = lien retiré (NULL — 0, le défaut WinDev, violerait la clé
    // étrangère), sinon l'id.
    public static Opt<long?> OptionalId(Opt<string> value) =>
        value.IsSet ? new Opt<long?>(value.Value.Length > 0 ? Id(value.Value) : null) : default;

    // `dto.X ? BigInt(dto.X) : null` : absent ou vide → NULL.
    public static long? IdOrNull(Opt<string> value) => value.IsSet && value.Value.Length > 0 ? Id(value.Value) : null;

    public static void ApplyInt(this Opt<double> value, Action<int> assign)
    {
        if (value.IsSet) assign(Int(value.Value));
    }

    // Champ date d'un formulaire : absent = inchangé, "" = colonne vidée
    // (NULL), sinon la date. dateOnly : colonne DATE (jour UTC, sans heure).
    public static void ApplyDate(this Opt<string> value, Action<DateTime?> assign, bool dateOnly = false)
    {
        if (!value.IsSet) return;
        if (value.Value.Length == 0) assign(null);
        else assign(dateOnly ? Date(value.Value).Date : Date(value.Value));
    }

    // Jour d'une date reçue (ses 10 premiers caractères, « AAAA-MM-JJ ») →
    // minuit UTC, pour une colonne DATE ; vide = NULL. Refusé en 400 sinon.
    public static DateTime? ToDay(string? raw, string field) =>
        string.IsNullOrEmpty(raw) ? null : Params.ParseDay(raw.Length > 10 ? raw[..10] : raw, field);

    // Date reçue (voir [Date], déjà validée) → instant UTC.
    public static DateTime Date(string value) =>
        JsDate.TryParse(value, out var date) ? date : throw new InvalidOperationException($"Date invalide : {value}");
}

public static class QueryExtensions
{
    // Équivalent de findUniqueOrThrow : 404 « Enregistrement introuvable. »
    public static async Task<T> FirstOrThrowAsync<T>(this IQueryable<T> query, CancellationToken ct = default) =>
        await query.FirstOrDefaultAsync(ct) ?? throw new RecordNotFoundException();

    // Équivalent de delete : 404 si la ligne n'existe pas.
    public static async Task DeleteOrThrowAsync<T>(this IQueryable<T> query, CancellationToken ct = default)
    {
        if (await query.ExecuteDeleteAsync(ct) == 0) throw new RecordNotFoundException();
    }
}

// Base des contrôleurs : comme NestJS, un POST répond 201 par défaut.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected async Task<IActionResult> Created201(Task<object> result) => StatusCode(StatusCodes.Status201Created, await result);
}
