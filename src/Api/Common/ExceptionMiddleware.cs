using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Api.Common;

// { statusCode, message } partout, 500 sans détail pour l'inattendu
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
                // trop tard pour répondre
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

        // corps illisible ou trop gros (Kestrel)
        if (exception is BadHttpRequestException bad)
        {
            return bad.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? (bad.StatusCode, "request entity too large")
                : (bad.StatusCode, bad.Message);
        }

        if (IsDatabaseUnavailable(exception))
        {
            // seule trace de la vraie cause
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
            logger.LogWarning("MySQL {Code} {Detail}", mysql.Number, mysql.Message);
            return known;
        }

        // une ligne, sinon Cloud Logging découpe en plusieurs entrées
        logger.LogError("{Detail}", OneLine(exception.ToString()));
        return (StatusCodes.Status500InternalServerError, InternalErrorMessage);
    }

    private static readonly Dictionary<int, (int Status, string Message)> MySqlErrors = new()
    {
        // ER_DATA_TOO_LONG
        [1406] = (StatusCodes.Status400BadRequest, "Une valeur est trop longue pour son champ."),
        // ER_DUP_ENTRY
        [1062] = (StatusCodes.Status409Conflict, "Un enregistrement avec cette valeur existe déjà."),
        // ER_NO_REFERENCED_ROW(_2), ER_ROW_IS_REFERENCED(_2)
        [1216] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1217] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1451] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
        [1452] = (StatusCodes.Status409Conflict, ForeignKeyMessage),
    };

    private const string ForeignKeyMessage = "Opération impossible : un enregistrement lié est introuvable ou encore utilisé ailleurs.";

    // base injoignable, mauvais identifiants, trop de connexions... : 503
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
