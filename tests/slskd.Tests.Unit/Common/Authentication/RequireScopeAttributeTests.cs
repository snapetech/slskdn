// <copyright file="RequireScopeAttributeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Common.Authentication;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using slskd;
using slskd.Authentication;
using Xunit;

// HARDENING-2026-04-20 H13: verifies the scope-based authorization filter powering the scoped
// NowPlaying webhook key. The attribute must 403 an authenticated principal whose scope claims
// don't include the required tag (and don't include the wildcard), while allowing everything
// else through — including anonymous requests, which the earlier [Authorize] gate handles.
public class RequireScopeAttributeTests
{
    [Fact]
    public void Constructor_RejectsEmptyScope()
    {
        Assert.Throws<ArgumentException>(() => new RequireScopeAttribute(string.Empty));
        Assert.Throws<ArgumentException>(() => new RequireScopeAttribute(" "));
    }

    [Fact]
    public void AnonymousRequest_IsNotChallenged()
    {
        // Not this filter's job to 401 — the [Authorize] attribute upstream does that.
        var ctx = BuildContext(user: new ClaimsPrincipal(new ClaimsIdentity()));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void PrincipalWithNoScopeClaims_IsTreatedAsUniversal()
    {
        var ctx = BuildContext(BuildPrincipal(/* no scope claims */));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void PrincipalWithWildcardScope_IsAllowed()
    {
        var ctx = BuildContext(BuildPrincipal(SlskdClaims.ScopeAll));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void PrincipalWithMatchingScope_IsAllowed()
    {
        var ctx = BuildContext(BuildPrincipal("nowplaying"));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void PrincipalWithMatchingScope_IgnoresCase()
    {
        var ctx = BuildContext(BuildPrincipal("NowPlaying"));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void PrincipalWithOnlyUnrelatedScopes_IsDenied()
    {
        var ctx = BuildContext(BuildPrincipal("federation", "admin"));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void PrincipalWithMixedScopes_IncludingMatch_IsAllowed()
    {
        var ctx = BuildContext(BuildPrincipal("federation", "nowplaying"));
        new RequireScopeAttribute("nowplaying").OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    private static ClaimsPrincipal BuildPrincipal(params string[] scopes)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "test"),
        };

        foreach (var scope in scopes)
        {
            claims.Add(new Claim(SlskdClaims.ScopeClaim, scope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "ApiKey"));
    }

    private static AuthorizationFilterContext BuildContext(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user,
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, Array.Empty<IFilterMetadata>());
    }
}
