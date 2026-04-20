// <copyright file="RequireScopeAttribute.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Authentication;

using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
///     Demands that the authenticated principal carry a specific scope claim
///     (<see cref="SlskdClaims.ScopeClaim"/>) or the wildcard scope
///     (<see cref="SlskdClaims.ScopeAll"/>).
/// </summary>
/// <remarks>
///     <para>
///         HARDENING-2026-04-20 H13: the legacy model grants every API key access to every
///         endpoint that only requires authentication. That forces operators who want to wire
///         Plex / Jellyfin / Tautulli into the NowPlaying webhook to hand the media server a
///         full-power API key — which gets logged by reverse proxies, inlined into Plex webhook
///         URLs, and persisted in their config. This attribute lets a key be minted with a
///         narrow <c>scopes</c> list (e.g. just <c>nowplaying</c>) and still satisfy the
///         standard <c>[Authorize]</c> gate, without granting the rest of the API.
///     </para>
///     <para>
///         Apply on top of an <c>[Authorize]</c> attribute — this filter does not authenticate,
///         it only inspects claims on the already-authenticated principal.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequireScopeAttribute : Attribute, IAuthorizationFilter
{
    public RequireScopeAttribute(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope must be non-empty", nameof(scope));
        }

        Scope = scope;
    }

    /// <summary>Gets the scope tag required by this endpoint.</summary>
    public string Scope { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var user = context.HttpContext?.User;

        // No identity / unauthenticated → let the earlier auth middleware's 401 stand;
        // RequireScope is a 403 (authenticated but not scoped) layer.
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            return;
        }

        var heldScopes = user.FindAll(SlskdClaims.ScopeClaim).Select(c => c.Value).ToArray();

        // Backward compatibility: a principal with NO scope claims at all is treated as
        // universal. This matches how the ApiKey handler / JWT generator populate `*` for
        // legacy keys, and guards against tests / mocks that don't inject scope claims.
        if (heldScopes.Length == 0)
        {
            return;
        }

        if (heldScopes.Contains(SlskdClaims.ScopeAll, StringComparer.OrdinalIgnoreCase) ||
            heldScopes.Contains(Scope, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            error = "insufficient_scope",
            required_scope = Scope,
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
