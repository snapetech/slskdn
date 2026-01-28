// <copyright file="ISolidClientIdDocumentService.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

/// <summary>
///     Service for generating Solid-OIDC Client ID documents.
/// </summary>
public interface ISolidClientIdDocumentService
{
    /// <summary>
    ///     Writes a compliant JSON-LD Client ID document to the HTTP response.
    /// </summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteClientIdDocumentAsync(HttpContext http, CancellationToken ct);
}
