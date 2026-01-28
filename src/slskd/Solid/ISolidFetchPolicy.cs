// <copyright file="ISolidFetchPolicy.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     SSRF hardening policy for WebID/Pod fetches.
/// </summary>
public interface ISolidFetchPolicy
{
    /// <summary>
    ///     Validates a URI against SSRF hardening rules (HTTPS enforcement, host allow-list, private IP blocking).
    /// </summary>
    /// <param name="uri">The URI to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the URI violates SSRF hardening rules.</exception>
    Task ValidateAsync(Uri uri, CancellationToken ct);
}
