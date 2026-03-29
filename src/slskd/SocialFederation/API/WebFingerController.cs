// <copyright file="WebFingerController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation.API
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd.Core.Security;

    /// <summary>
    ///     WebFinger endpoint for ActivityPub discovery.
    /// </summary>
    /// <remarks>
    ///     T-FED01: WebFinger endpoint for actor discovery.
    ///     RFC 7033 WebFinger protocol implementation for ActivityPub.
    /// </remarks>
    [ApiController]
    [Route(".well-known")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class WebFingerController : ControllerBase
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly LibraryActorService _libraryActorService;
        private readonly ILogger<WebFingerController> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="WebFingerController"/> class.
        /// </summary>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="libraryActorService">The library actor service.</param>
        /// <param name="logger">The logger.</param>
        public WebFingerController(
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            LibraryActorService libraryActorService,
            ILogger<WebFingerController> logger)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _libraryActorService = libraryActorService ?? throw new ArgumentNullException(nameof(libraryActorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Handles WebFinger requests for actor discovery.
        /// </summary>
        /// <param name="resource">The resource identifier (acct: or https: URI).</param>
        /// <param name="rel">Optional relationship filter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The WebFinger JRD document.</returns>
        /// <remarks>
        ///     GET /.well-known/webfinger?resource=acct:username@domain
        ///     Returns JRD (JSON Resource Descriptor) with actor links.
        /// </remarks>
        [HttpGet("webfinger")]
        [AllowAnonymous]
        [Produces("application/jrd+json")]
        public async Task<IActionResult> GetWebFinger(
            [FromQuery] string resource,
            [FromQuery] string? rel = null,
            CancellationToken cancellationToken = default)
        {
            resource = resource?.Trim() ?? string.Empty;
            rel = string.IsNullOrWhiteSpace(rel) ? null : rel.Trim();

            var opts = _federationOptions.CurrentValue;

            // Check if federation is enabled and not in hermit mode
            if (!opts.Enabled || opts.IsHermit)
            {
                _logger.LogDebug("[WebFinger] Federation disabled or in hermit mode, returning 404");
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(resource))
            {
                _logger.LogDebug("[WebFinger] Missing resource parameter");
                return BadRequest("Missing resource parameter");
            }

            // Parse the resource identifier
            if (!TryParseResource(resource, out var username, out var domain))
            {
                _logger.LogDebug("[WebFinger] Invalid resource format: {Resource}", resource);
                return NotFound();
            }

            // Verify domain matches our federation domain
            if (!string.Equals(domain, opts.Domain, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[WebFinger] Domain mismatch: {Domain} != {ExpectedDomain}", domain, opts.Domain);
                return NotFound();
            }

            // Check if this is a valid actor/username
            if (!await IsValidActorAsync(username, cancellationToken))
            {
                _logger.LogDebug("[WebFinger] Invalid actor: {Username}", username);
                return NotFound();
            }

            var actorId = $"{opts.BaseUrl}/actors/{username}";
            var jrd = new WebFingerJrd
            {
                Subject = resource,
                Links = new[]
                {
                    new WebFingerLink
                    {
                        Rel = "self",
                        Type = "application/activity+json",
                        Href = actorId
                    },
                    new WebFingerLink
                    {
                        Rel = "http://webfinger.net/rel/profile-page",
                        Type = "text/html",
                        Href = $"{opts.BaseUrl}/@{username}"
                    }
                }
            };

            // Filter by rel if specified
            if (!string.IsNullOrWhiteSpace(rel))
            {
                jrd.Links = jrd.Links.Where(link => link.Rel == rel).ToArray();
            }

            _logger.LogInformation("[WebFinger] Served WebFinger for {Resource}", resource);
            return Ok(jrd);
        }

        private static bool TryParseResource(string resource, out string username, out string domain)
        {
            username = string.Empty;
            domain = string.Empty;
            resource = resource.Trim();

            // Handle acct: URIs (acct:username@domain)
            if (resource.StartsWith("acct:", StringComparison.OrdinalIgnoreCase))
            {
                var acctPart = resource.Substring(5); // Remove "acct:"
                var atIndex = acctPart.LastIndexOf('@');
                if (atIndex <= 0 || atIndex >= acctPart.Length - 1)
                {
                    return false;
                }

                username = acctPart.Substring(0, atIndex).Trim();
                domain = acctPart.Substring(atIndex + 1).Trim();
                return !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(domain);
            }

            // Handle https: URIs (https://domain/@username or https://domain/actors/username)
            if (resource.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(resource, UriKind.Absolute, out var uri) ||
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                domain = uri.Host;

                var pathSegments = uri.AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (pathSegments.Length == 1 && pathSegments[0].StartsWith("@", StringComparison.Ordinal))
                {
                    username = pathSegments[0][1..].Trim();
                    return !string.IsNullOrWhiteSpace(username);
                }

                if (pathSegments.Length == 2 &&
                    string.Equals(pathSegments[0], "actors", StringComparison.OrdinalIgnoreCase))
                {
                    username = pathSegments[1].Trim();
                    return !string.IsNullOrWhiteSpace(username);
                }

                return false;
            }

            return false;
        }

        private Task<bool> IsValidActorAsync(string username, CancellationToken cancellationToken)
        {
            return Task.FromResult(_libraryActorService.IsLibraryActor(username));
        }

        /// <summary>
        ///     WebFinger JRD (JSON Resource Descriptor) response.
        /// </summary>
        public sealed class WebFingerJrd
        {
            /// <summary>
            ///     Gets or sets the subject resource identifier.
            /// </summary>
            public string Subject { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the array of links.
            /// </summary>
            public WebFingerLink[] Links { get; set; } = Array.Empty<WebFingerLink>();
        }

        /// <summary>
        ///     WebFinger link object.
        /// </summary>
        public sealed class WebFingerLink
        {
            /// <summary>
            ///     Gets or sets the relationship type.
            /// </summary>
            public string Rel { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the MIME type.
            /// </summary>
            public string? Type { get; set; }

            /// <summary>
            ///     Gets or sets the target URI.
            /// </summary>
            public string Href { get; set; } = string.Empty;
        }
    }
}
