// <copyright file="ISolidWebIdResolver.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Service for resolving WebID profiles and extracting OIDC issuers.
/// </summary>
public interface ISolidWebIdResolver
{
    /// <summary>
    ///     Resolves a WebID profile and extracts OIDC issuer information.
    /// </summary>
    /// <param name="webId">The WebID URI to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved WebID profile with OIDC issuers.</returns>
    Task<SolidWebIdProfile> ResolveAsync(Uri webId, CancellationToken ct);
}

/// <summary>
///     Resolved WebID profile information.
/// </summary>
/// <param name="WebId">The WebID URI.</param>
/// <param name="OidcIssuers">Array of OIDC issuer URIs found in the profile.</param>
public sealed record SolidWebIdProfile(Uri WebId, Uri[] OidcIssuers);
