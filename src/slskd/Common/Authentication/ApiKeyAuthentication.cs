// <copyright file="ApiKeyAuthentication.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// <copyright file="ApiKeyAuthentication.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>
using Microsoft.Extensions.Options;

namespace slskd.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Security.Principal;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     API key authentication.
    /// </summary>
    public static class ApiKeyAuthentication
    {
        /// <summary>
        ///     Gets the API key authentication scheme name.
        /// </summary>
        public static string AuthenticationScheme { get; } = "ApiKey";
    }

    /// <summary>
    ///     Handles API key authentication.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="apiKeyOptionsMonitor">An options monitor.</param>
        /// <param name="securityService">The security service.</param>
        /// <param name="logger">A logger factory.</param>
        /// <param name="urlEncoder">A url encoder.</param>
        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptionsMonitor,
            ISecurityService securityService,
            ILoggerFactory logger,
            UrlEncoder urlEncoder)
            : base(apiKeyOptionsMonitor, logger, urlEncoder)
        {
            Security = securityService;
        }

        private ISecurityService Security { get; }

        /// <summary>
        ///     Authenticates via API key.
        /// </summary>
        /// <returns>A successful authentication result containing a ticket for the API key.</returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Task.Yield();

            if (!Request.Headers.TryGetValue("X-API-Key", out var headerKeyValue))
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
                if (remoteIpAddress == null)
                {
                    return AuthenticateResult.Fail("Remote IP address is unavailable");
                }

                var apiKey = headerKeyValue.ToString();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return AuthenticateResult.Fail("API key header is empty");
                }

                var key = Security.AuthenticateWithApiKey(apiKey, remoteIpAddress);

                // HARDENING-2026-04-20 H13: embed scopes as claims so RequireScopeAttribute can
                // enforce per-endpoint restrictions without having to reach back into SecurityService.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, key.Name),
                    new Claim(ClaimTypes.Role, key.Role.ToString()),
                };

                foreach (var scope in key.Scopes ?? new[] { SlskdClaims.ScopeAll })
                {
                    claims.Add(new Claim(SlskdClaims.ScopeClaim, scope));
                }

                var identity = new ClaimsIdentity(claims, ApiKeyAuthentication.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), ApiKeyAuthentication.AuthenticationScheme);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                return AuthenticateResult.Fail(ex);
            }
        }
    }

    /// <summary>
    ///     API key authentication options.
    /// </summary>
#pragma warning disable S2094 // Classes should not be empty
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
#pragma warning restore S2094 // Classes should not be empty
    {
    }
}
