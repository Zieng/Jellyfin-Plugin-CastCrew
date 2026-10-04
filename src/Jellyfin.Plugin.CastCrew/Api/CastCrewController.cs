using System.Security.Claims;
using Jellyfin.Plugin.CastCrew.Services;
using Jellyfin.Plugin.CastCrew.Metadata;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Api;

[ApiController]
[Authorize]
[Route("CastCrew")]
public sealed class CastCrewController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly CastCrewActorQueryService _actorQueryService;
    private readonly CastCrewLibraryPersonMappingService _mappingService;
    private readonly ILogger<CastCrewController> _logger;
    private readonly MetadataSearchService _metadataSearchService;

    public CastCrewController(
        IUserManager userManager,
        CastCrewActorQueryService actorQueryService,
        CastCrewLibraryPersonMappingService mappingService,
        ILogger<CastCrewController> logger,
        MetadataSearchService metadataSearchService)
    {
        _userManager = userManager;
        _actorQueryService = actorQueryService;
        _mappingService = mappingService;
        _logger = logger;
        _metadataSearchService = metadataSearchService;
    }

    /// <summary>
    /// Serves a Jellyfin-compatible plugin repository manifest so the dashboard
    /// can resolve this plugin without an external repository.
    /// </summary>
    [HttpGet("Manifest")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public ActionResult GetRepositoryManifest()
    {
        var plugin = CastCrewPlugin.Instance;
        var version = plugin?.Version?.ToString() ?? "unknown";
        var description = plugin?.Description ?? "Adds a Cast & Crew module to the Jellyfin sidebar for browsing actors, directors, producers, and other crew members.";

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Serving CastCrew manifest metadata. Version={Version}.",
            version);

        var manifest = new[]
        {
            new
            {
                guid = "a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a",
                name = "CastCrew",
                description,
                overview = description,
                owner = "CastCrew",
                category = "General",
                versions = new[]
                {
                    new
                    {
                        version,
                        changelog = "Cast & Crew plugin with multi-version Jellyfin support (10.10.x and 10.11.x).",
                        targetAbi = "10.10.7.0",
                        sourceUrl = string.Empty,
                        checksum = string.Empty,
                        timestamp = "2026-06-27T00:00:00Z"
                    }
                }
            }
        };

        return Ok(manifest);
    }

    /// <summary>
    /// Returns the list of available media libraries that can be used for filtering.
    /// </summary>
    [HttpGet("Metadata/Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MetadataSearchResponse>> SearchMetadata(
        [FromQuery] MetadataPersonType type = MetadataPersonType.Actor,
        [FromQuery] string? name = null,
        [FromQuery] int? minAge = null, [FromQuery] int? maxAge = null,
        [FromQuery] decimal? minHeight = null, [FromQuery] decimal? maxHeight = null,
        [FromQuery] decimal? minBust = null, [FromQuery] decimal? maxBust = null,
        [FromQuery] decimal? minWaist = null, [FromQuery] decimal? maxWaist = null,
        [FromQuery] decimal? minHip = null, [FromQuery] decimal? maxHip = null,
        [FromQuery] int? minVideoCount = null, [FromQuery] int? maxVideoCount = null,
        [FromQuery] int startIndex = 0, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!(User?.Identity?.IsAuthenticated ?? false))
            return Unauthorized();

        var query = new MetadataSearchQuery
        {
            Type = type, Name = name, MinAge = minAge, MaxAge = maxAge,
            MinHeight = minHeight, MaxHeight = maxHeight, MinBust = minBust, MaxBust = maxBust,
            MinWaist = minWaist, MaxWaist = maxWaist, MinHip = minHip, MaxHip = maxHip,
            MinVideoCount = minVideoCount, MaxVideoCount = maxVideoCount,
            StartIndex = Math.Max(0, startIndex), Limit = Math.Clamp(limit, 1, 200)
        };
        return Ok(await _metadataSearchService.SearchAsync(query, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult GetLibraries()
    {
        var libraries = _mappingService.GetAvailableLibraries();
        CastCrewDebugLogging.LogInformation(
            _logger,
            "Listing available libraries for configuration. Count={Count}.",
            libraries.Count);

        var result = new List<object>();
        foreach (var lib in libraries)
        {
            result.Add(new { lib.Id, lib.Name, lib.CollectionType });
        }
        return Ok(result);
    }

    /// <summary>
    /// Triggers a rebuild of the person-to-library mapping.
    /// </summary>
    [HttpPost("Libraries/RefreshMapping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult RefreshLibraryMapping([FromQuery] string? reason = null)
    {
        var refreshReason = string.IsNullOrWhiteSpace(reason) ? "manual/api" : reason.Trim();
        CastCrewDebugLogging.LogInformation(
            _logger,
            "Received person-to-library mapping refresh request. Reason={Reason}.",
            refreshReason);

        var configuration = CastCrewPlugin.Instance?.Configuration;
        if (configuration?.EnableDebugLogging == true)
        {
            var includedLibraryIds = configuration.IncludedLibraryIds ?? Array.Empty<string>();
            var includedLabel = includedLibraryIds.Length == 0
                ? "(none configured; all libraries included)"
                : string.Join(", ", includedLibraryIds);

            if (refreshReason.Equals("settings-save", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[CastCrew][Debug] CastCrew settings saved, trigger person-to-library mapping.");
            }

            _logger.LogInformation(
                "[CastCrew][Debug] Trigger person-to-library mapping (reason: {Reason}). Included libraries: {IncludedLibraries}.",
                refreshReason,
                includedLabel);
        }

        _mappingService.RebuildMapping();
        CastCrewDebugLogging.LogInformation(
            _logger,
            "Person-to-library mapping refresh finished. LastBuildTimeUtc={LastBuildTimeUtc}.",
            _mappingService.LastBuildTime);

        return NoContent();
    }

    [HttpGet("Actors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CastCrewActorsResponse> GetActors([FromQuery] CastCrewActorsQuery query)
    {
        var user = GetAuthenticatedUser(query.UserId);
        if (user is null)
        {
            CastCrewDebugLogging.LogInformation(_logger, "Actors query rejected: no authenticated user context.");
            return Unauthorized();
        }

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Actors query accepted. StartIndex={StartIndex}, Limit={Limit}, SearchTerm={SearchTerm}, LibraryIds={LibraryIds}.",
            query.StartIndex ?? 0,
            query.Limit ?? 0,
            string.IsNullOrWhiteSpace(query.SearchTerm) ? "(none)" : query.SearchTerm.Trim(),
            string.IsNullOrWhiteSpace(query.LibraryIds) ? "(all)" : query.LibraryIds.Trim());

        var response = _actorQueryService.QueryActors(query, user);
        return Ok(response);
    }

    [HttpGet("Directors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CastCrewActorsResponse> GetDirectors([FromQuery] CastCrewActorsQuery query)
    {
        var user = GetAuthenticatedUser(query.UserId);
        if (user is null)
        {
            CastCrewDebugLogging.LogInformation(_logger, "Directors query rejected: no authenticated user context.");
            return Unauthorized();
        }

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Directors query accepted. StartIndex={StartIndex}, Limit={Limit}, SearchTerm={SearchTerm}, LibraryIds={LibraryIds}.",
            query.StartIndex ?? 0,
            query.Limit ?? 0,
            string.IsNullOrWhiteSpace(query.SearchTerm) ? "(none)" : query.SearchTerm.Trim(),
            string.IsNullOrWhiteSpace(query.LibraryIds) ? "(all)" : query.LibraryIds.Trim());

        var response = _actorQueryService.QueryDirectors(query, user);
        return Ok(response);
    }

    [HttpGet("Producers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CastCrewActorsResponse> GetProducers([FromQuery] CastCrewActorsQuery query)
    {
        var user = GetAuthenticatedUser(query.UserId);
        if (user is null)
        {
            CastCrewDebugLogging.LogInformation(_logger, "Producers query rejected: no authenticated user context.");
            return Unauthorized();
        }

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Producers query accepted. StartIndex={StartIndex}, Limit={Limit}, SearchTerm={SearchTerm}, LibraryIds={LibraryIds}.",
            query.StartIndex ?? 0,
            query.Limit ?? 0,
            string.IsNullOrWhiteSpace(query.SearchTerm) ? "(none)" : query.SearchTerm.Trim(),
            string.IsNullOrWhiteSpace(query.LibraryIds) ? "(all)" : query.LibraryIds.Trim());

        var response = _actorQueryService.QueryProducers(query, user);
        return Ok(response);
    }

#if JELLYFIN_10_11
    private Jellyfin.Database.Implementations.Entities.User? GetAuthenticatedUser(Guid? queryUserId)
#else
    private Jellyfin.Data.Entities.User? GetAuthenticatedUser(Guid? queryUserId)
#endif
    {
        var userIdFromClaims = GetUserIdFromClaims(User);
        var effectiveUserId = userIdFromClaims ?? queryUserId;

        if (effectiveUserId is null)
        {
            CastCrewDebugLogging.LogInformation(_logger, "Unable to resolve authenticated user id from claims/query.");
            return null;
        }

        CastCrewDebugLogging.LogInformation(_logger, "Resolved authenticated user context for CastCrew query.");
        return _userManager.GetUserById(effectiveUserId.Value);
    }

    private static Guid? GetUserIdFromClaims(ClaimsPrincipal principal)
    {
        // Try multiple claim types for cross-version compatibility
        string[] userIdClaimTypes = { "Jellyfin-UserId", "UserId", System.Security.Claims.ClaimTypes.NameIdentifier };

        foreach (var claimType in userIdClaimTypes)
        {
            var userIdClaim = principal.Claims.FirstOrDefault(claim =>
                claim.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));

            if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
        }

        return null;
    }
}
