using Api.Features.DocumentMerge;
using Api.Features.Mistral;
using Api.Features.Performance;

namespace Api.Features;

// Enregistrement des services métier dans l'injection de dépendances
// (équivalent du tableau `imports` de app.module.ts). Par convention, toute
// classe `XxxService` déclarée sous Api.Features est enregistrée avec une
// durée de vie « par requête » (comme le DbContext qu'elle utilise) : pour
// ajouter une fonctionnalité, il suffit de créer son dossier sous Features/.
// Les services qui gardent un état entre les requêtes sont déclarés à part,
// en singleton.
public static class FeatureServices
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        // Une seule instance de Chromium pour toute l'application.
        services.AddSingleton<PdfRendererService>();
        // Avancement de l'impression de tous les contrats, partagé entre les requêtes.
        services.AddSingleton<ImpressionState>();
        // Client HTTP de l'API Mistral (OCR), sans limite de durée courte : un
        // gros PDF peut demander plusieurs minutes.
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
