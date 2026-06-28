using System.Text.Json;
using System.Text.Json.Nodes;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

/// <summary>
/// ASP.NET Core middleware that intercepts requests to /web/config.json
/// and injects the Cast&amp;Crew menu link into the response.
/// This allows the sidebar entry to appear even when the web root is read-only
/// (e.g., Docker containers).
/// </summary>
internal sealed class CastCrewConfigJsonMiddleware
{
    private const string ConfigJsonPath = "/web/config.json";
    private const string CastCrewLinkName = "Cast&Crew";
    private const string CastCrewLinkIcon = "person";
    private const string CastCrewLinkUrl = "/web/#/home?tab=cast_crew";

    private readonly RequestDelegate _next;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<CastCrewConfigJsonMiddleware> _logger;

    public CastCrewConfigJsonMiddleware(
        RequestDelegate next,
        IApplicationPaths applicationPaths,
        ILogger<CastCrewConfigJsonMiddleware> logger)
    {
        _next = next;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only intercept GET requests to /web/config.json
        if (!HttpMethods.IsGet(context.Request.Method) ||
            !string.Equals(context.Request.Path.Value, ConfigJsonPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var configuration = CastCrewPlugin.Instance?.Configuration;
        if (configuration is null || !configuration.EnableCastCrewMainMenuEntry)
        {
            await _next(context);
            return;
        }

        // Read the original config.json from the web root
        var configFilePath = Path.Combine(_applicationPaths.WebPath, "config.json");
        if (!File.Exists(configFilePath))
        {
            await _next(context);
            return;
        }

        try
        {
            var configText = await File.ReadAllTextAsync(configFilePath);
            var root = JsonNode.Parse(configText) as JsonObject;
            if (root is null)
            {
                await _next(context);
                return;
            }

            // Ensure menuLinks array exists
            var menuLinks = root["menuLinks"] as JsonArray;
            if (menuLinks is null)
            {
                menuLinks = new JsonArray();
                root["menuLinks"] = menuLinks;
            }

            // Check if Cast&Crew link already exists
            var hasLink = false;
            foreach (var node in menuLinks)
            {
                if (node is JsonObject link)
                {
                    var name = link["name"]?.GetValue<string>();
                    var url = link["url"]?.GetValue<string>();
                    if (string.Equals(name, CastCrewLinkName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(url, CastCrewLinkUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        hasLink = true;
                        break;
                    }
                }
            }

            if (!hasLink)
            {
                menuLinks.Add(new JsonObject
                {
                    ["name"] = CastCrewLinkName,
                    ["icon"] = CastCrewLinkIcon,
                    ["url"] = CastCrewLinkUrl
                });
            }

            // Serve the modified config.json
            var modifiedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(modifiedJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CastCrew] Failed to inject menu link into config.json response, falling through to static file.");
            await _next(context);
        }
    }
}

/// <summary>
/// Registers the config.json middleware early in the ASP.NET pipeline via IStartupFilter.
/// This ensures the middleware runs BEFORE the static file middleware.
/// </summary>
internal sealed class CastCrewStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<CastCrewConfigJsonMiddleware>();
            next(app);
        };
    }
}
