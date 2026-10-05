namespace Api.Common;

// En-têtes de sécurité HTTP : les mêmes que ceux posés par helmet côté
// NestJS (`helmet({ crossOriginResourcePolicy: { policy: 'cross-origin' } })`).
// Cross-Origin-Resource-Policy est en « cross-origin » : le front, servi
// depuis une autre origine, lit les réponses de l'API.
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self';base-uri 'self';font-src 'self' https: data:;form-action 'self';frame-ancestors 'self';img-src 'self' data:;object-src 'none';script-src 'self';script-src-attr 'none';style-src 'self' https: 'unsafe-inline';upgrade-insecure-requests";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        // La page Swagger (développement) embarque ses propres scripts.
        if (!context.Request.Path.StartsWithSegments("/swagger")) headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        headers["Origin-Agent-Cluster"] = "?1";
        headers["Referrer-Policy"] = "no-referrer";
        headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        headers.XContentTypeOptions = "nosniff";
        headers["X-DNS-Prefetch-Control"] = "off";
        headers["X-Download-Options"] = "noopen";
        headers.XFrameOptions = "SAMEORIGIN";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers.XXSSProtection = "0";
        return next(context);
    }
}
