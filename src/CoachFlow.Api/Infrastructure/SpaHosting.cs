using Microsoft.Net.Http.Headers;

namespace CoachFlow.Api.Infrastructure;

public static class SpaHosting
{
    // Diese Dateien steuern PWA-Updates und dürfen nicht langfristig im Browser liegen.
    private static readonly string[] NoCacheFiles =
    [
        "/index.html",
        "/sw.js",
        "/registerSW.js",
        "/manifest.webmanifest"
    ];

    // Der Fallback-Endpunkt braucht dieselben Optionen, sonst fehlt index.html die Cache-Regel.
    private static readonly StaticFileOptions FileOptions = new()
    {
        OnPrepareResponse = context =>
        {
            var path = context.Context.Request.Path.Value ?? "";
            var headers = context.Context.Response.Headers;

            if (NoCacheFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                headers[HeaderNames.CacheControl] = "no-cache";
            }
            else if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            {
                headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
            }
        }
    };

    public static void UseCoachFlowSpa(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(FileOptions);
    }

    public static void MapCoachFlowSpaFallback(this WebApplication app)
    {
        // Unbekannte API-Pfade bleiben 404 und liefern nicht die Single-Page-Anwendung aus.
        app.Map("/api/{**rest}", () => Results.NotFound());
        app.MapFallbackToFile("index.html", FileOptions);
    }
}
