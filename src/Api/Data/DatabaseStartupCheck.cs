using Microsoft.EntityFrameworkCore;

namespace Api.Data;

// juste pour logger la cause exacte, ne bloque pas le démarrage
public static class DatabaseStartupCheck
{
    public static async Task LogAsync(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();
            logger.LogInformation("Connexion à la base de données réussie.");
        }
        catch (Exception error)
        {
            logger.LogError("Connexion à la base de données impossible : {Message}", error.Message);
        }
    }
}
