using System.Security.Claims;
using System.Text.Json;
using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CastCrew.Api;

[ApiController]
[Authorize]
[Route("CastCrew")]
public sealed class CastCrewController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly CastCrewActorQueryService _actorQueryService;

    public CastCrewController(IUserManager userManager, CastCrewActorQueryService actorQueryService)
    {
        _userManager = userManager;
        _actorQueryService = actorQueryService;
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

    [HttpGet("Actors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CastCrewActorsResponse> GetActors([FromQuery] CastCrewActorsQuery query)
    {
        var user = GetAuthenticatedUser(query.UserId);
        if (user is null)
        {
            return Unauthorized();
        }

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
            return Unauthorized();
        }

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
            return Unauthorized();
        }

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
            return null;
        }

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
