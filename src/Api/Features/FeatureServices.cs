using Api.Features.DocumentMerge;
using Api.Features.Mistral;
using Api.Features.Performance;

namespace Api.Features;

// XxxService sous Api.Features : scoped automatiquement, singletons à la main
public static class FeatureServices
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        // un seul Chromium pour toute l'appli
        services.AddSingleton<PdfRendererService>();
        // avancement de l'impression, partagé entre requêtes
        services.AddSingleton<ImpressionState>();
        // OCR d'un gros PDF : plusieurs minutes
        services.AddHttpClient<MistralService>(client => client.Timeout = TimeSpan.FromMinutes(10));

        var serviceTypes = typeof(FeatureServices).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false }
                && type.Namespace?.StartsWith("Api.Features", StringComparison.Ordinal) == true
                && type.Name.EndsWith("Service", StringComparison.Ordinal));
        foreach (var type in serviceTypes)
        {
            if (services.All(descriptor => descriptor.ServiceType != type)) services.AddScoped(type);
        }
        return services;
    }
}
