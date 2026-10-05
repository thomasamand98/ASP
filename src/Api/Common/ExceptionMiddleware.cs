using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Api.Common;

// Format d'erreur unique pour toute l'API : { statusCode, message }
// (équivalent de AllExceptionsFilter côté NestJS). Les erreurs volontaires
// (HttpException : 400, 401, 404, 409, 429...) gardent leur message ; les
// erreurs MySQL prévisibles (doublon, référence invalide, valeur trop
// longue...) sont traduites en statut et message clairs ; toute autre erreur
// (bug...) est journalisée côté serveur et renvoyée au client sans le moindre
// détail interne.
public partial class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public const string RecordNotFoundMessage = "Enregistrement introuvable.";
    public const string DatabaseUnavailableMessage = "Base de données indisponible, réessayez dans quelques instants.";
    public const string InternalErrorMessage = "Erreur interne du serveur";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (status, message) = Translate(exception, logger);
            if (context.Response.HasStarted)
            {
                // La réponse est déjà partie : on ne peut plus qu'interrompre.
                context.Abort();
                return;
            }
            await WriteAsync(context, status, message);
        }
    }

    public static Task WriteAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ErrorBody(status, message), JsonConfig.Options);
    }

    public static (int Status, string Message) Translate(Exception exception, ILogger logger)
    {
        if (exception is HttpException http) return (http.StatusCode, http.Message);

        // Corps illisible ou trop gros, rejeté par Kestrel.
        if (exception is BadHttpRequestException bad)
        {
            return bad.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? (bad.StatusCode, "request entity too large")
                : (bad.StatusCode, bad.Message);
        }

        if (IsDatabaseUnavailable(exception))
        {
            // Journalisé en erreur avec le détail : c'est la seule trace de la
            // cause réelle (mot de passe, socket, base...).
            logger.LogError("Base de données indisponible : {Detail}", OneLine(exception.ToString()));
            return (StatusCodes.Status503ServiceUnavailable, DatabaseUnavailableMessage);
        }

        if (exception is DbUpdateConcurrencyException)
        {
            logger.LogWarning("P2025 {Detail}", exception.Message);
            return (StatusCodes.Status404NotFound, RecordNotFoundMessage);
        }

        if (FindMySqlException(exception) is { } mysql && MySqlErrors.TryGetValue(mysql.Number, out var known))
        {
            // Journalisé en avertissement : utile pour repérer un appel du front
            // qui vise un id disparu ou une contrainte non anticipée.
            logger.LogWarning("MySQL {Code} {Detail}", mysql.Number, mysql.Message);
            return known;
        }

        // Sur une seule ligne : Cloud Logging crée une entrée par ligne, et le
        // détail utile (table ou colonne absente...) serait séparé de l'en-tête.
        logger.LogError("{Detail}", OneLine(exception.ToString()));
        return (StatusCodes.Status500InternalServerError, InternalErrorMessage);
    }

    // Codes d'erreur MySQL correspondant aux erreurs Prisma traduites par le
    // backend NestJS (P2000, P2002, P2003).
    private static readonly Dictionary<int, (int Status, string Message)> MySqlErrors = new()
    {
        // P2000 — ER_DATA_TOO_LONG
        [1406] = (StatusCodes.Status400BadRequest, "Une valeur est trop longue pour son champ."),
        // P2002 — ER_DUP_ENTRY
        [1062] = (StatusCodes.Status409Conflict, "Un enregistrement avec cette valeur existe déjà."),
        // P2003 — ER_ROW_IS_REFERENCED(_2), ER_NO_REFERENCED_ROW(_2)
        [1216] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1217] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1451] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1452] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
    };

    private const string ForeignKeyMessage = "Opération impossible : un enregistrement lié est introuvable ou encore utilisé ailleurs.";

    // Base injoignable ou qui refuse la connexion (identifiants, base absente,
    // serveur arrêté, trop de connexions...) : ce n'est pas un bug de l'API
    // mais une indisponibilité, d'où un 503 plutôt qu'un 500.
    private static readonly HashSet<int> UnavailableCodes = [1040, 1042, 1044, 1045, 1049, 1129, 1130, 1203, 2002, 2003, 2006, 2013];

    private static bool IsDatabaseUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException) return FindMySqlException(exception) is not null || exception is MySqlException;
            if (current is MySqlException mysql && (UnavailableCodes.Contains(mysql.Number) || mysql.Number == 0 && mysql.InnerException is not null)) return true;
        }
        return false;
    }

    private static MySqlException? FindMySqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql) return mysql;
        }
        return null;
    }

    private static string OneLine(string text) => Newlines().Replace(text, " | ");

    [GeneratedRegex(@"\s*\n\s*")]
    private static partial Regex Newlines();
}

public record ErrorBody(int statusCode, string message);
