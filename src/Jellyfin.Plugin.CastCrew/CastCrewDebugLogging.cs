using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

internal static class CastCrewDebugLogging
{
    internal static bool IsEnabled()
        => CastCrewPlugin.Instance?.Configuration?.EnableDebugLogging == true;

    internal static void LogInformation(ILogger? logger, string message, params object[] args)
    {
        if (!IsEnabled())
        {
            return;
        }

        if (logger is not null)
        {
            logger.LogInformation("[CastCrew][Debug] " + message, args);
            return;
        }

        if (args.Length == 0)
        {
            Console.Error.WriteLine("[CastCrew][Debug] " + message);
            return;
        }

        Console.Error.WriteLine("[CastCrew][Debug] " + string.Format(CultureInfo.InvariantCulture, message, args));
    }
}
