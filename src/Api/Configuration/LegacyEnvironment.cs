using MySql.Data.MySqlClient;

namespace Api.Configuration;

// Compatibilité avec la configuration du backend NestJS : les mêmes
// variables d'environnement (et le même fichier .env en développement)
// alimentent les options de appsettings.json. Un déploiement existant
// (Cloud Run, Railway) fonctionne donc sans changer ses variables.
public static class LegacyEnvironment
{
    private static readonly Dictionary<string, string> Mapping = new()
    {
        ["DATABASE_URL"] = "App:DatabaseUrl",
        ["CORS_ORIGIN"] = "App:CorsOrigin",
        ["TRUST_PROXY"] = "App:TrustProxy",
        ["RAILWAY_ENVIRONMENT"] = "App:RailwayEnvironment",
        ["CGV_URL"] = "App:CgvUrl",
        ["PDF_STORAGE_DIR"] = "App:PdfStorageDir",
        ["MISTRAL_API_KEY"] = "App:MistralApiKey",
        ["JWT_SECRET"] = "Auth:JwtSecret",
        ["COOKIE_SAMESITE"] = "Auth:CookieSameSite",
        ["COOKIE_SECURE"] = "Auth:CookieSecure",
        ["THROTTLE_LIMIT"] = "Auth:ThrottleLimit",
        ["LOGIN_THROTTLE_LIMIT"] = "Auth:LoginThrottleLimit",
        ["ACCESS_TOKEN_TTL_SECONDS"] = "Auth:AccessTokenTtlSeconds",
    };

    public static void AddLegacyEnvironment(this ConfigurationManager configuration, string contentRoot)
    {
        var values = new Dictionary<string, string?>();
        // Les variables du fichier .env ne remplacent jamais celles déjà
        // définies dans l'environnement (comme dotenv).
        // Fichier cherché dans le projet, puis à la racine du dépôt,
        // puis dans le backend NestJS voisin (le temps de la migration).
        string[] candidates = [".env", "../../.env", "../../backend/.env"];
        var file = ReadDotEnv(candidates.Select(path => Path.GetFullPath(Path.Combine(contentRoot, path))).FirstOrDefault(File.Exists) ?? "");
        foreach (var (name, key) in Mapping)
        {
            var value = Environment.GetEnvironmentVariable(name) ?? file.GetValueOrDefault(name);
            if (value is not null) values[key] = value;
        }
        var port = Environment.GetEnvironmentVariable("PORT") ?? file.GetValueOrDefault("PORT");
        if (!string.IsNullOrEmpty(port)) values["Port"] = port;
        configuration.AddInMemoryCollection(values);
    }

    private static Dictionary<string, string> ReadDotEnv(string path)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(path)) return result;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'')) value = value[1..^1];
            result[line[..separator].Trim()] = value;
        }
        return result;
    }

    // mysql://utilisateur:motdepasse@hote:3306/base?param=valeur → chaîne de
    // connexion MySQL. Paramètres reconnus : allowPublicKeyRetrieval, et
    // socketPath (socket Unix de Cloud SQL).
    public static string ToConnectionString(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl)) throw new InvalidOperationException("DATABASE_URL absente : aucune requête ne pourra aboutir.");
        if (!databaseUrl.Contains("://")) return databaseUrl;

        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = uri.Host,
            Port = (uint)(uri.Port > 0 ? uri.Port : 3306),
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            UserID = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            CharacterSet = "utf8mb4",
        };
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            switch (parts[0].ToLowerInvariant())
            {
                case "allowpublickeyretrieval":
                    builder.AllowPublicKeyRetrieval = value == "true";
                    break;
                case "socketpath" or "socket":
                    builder.Server = value;
                    builder.ConnectionProtocol = MySqlConnectionProtocol.UnixSocket;
                    break;
            }
        }
        return builder.ConnectionString;
    }
}
