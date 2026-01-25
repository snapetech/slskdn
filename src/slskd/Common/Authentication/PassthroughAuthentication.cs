// <copyright file="PassthroughAuthentication.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

﻿// <copyright file="PassthroughAuthentication.cs" company="slskd Team">
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

namespace slskd.Authentication
{
    using System.Net;
    using System.Security.Principal;
    using NetTools;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Passthrough authentication.
    /// </summary>
    public static class PassthroughAuthentication
    {
        /// <summary>
        ///     Gets the Passthrough authentication scheme name.
        /// </summary>
        public static string AuthenticationScheme { get; } = "Passthrough";
    }

    /// <summary>
    ///     Handles passthrough authentication.
    /// </summary>
    public class PassthroughAuthenticationHandler : AuthenticationHandler<PassthroughAuthenticationOptions>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PassthroughAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="optionsMonitor">An options monitor.</param>
        /// <param name="logger">A logger factory.</param>
        /// <param name="urlEncoder">A url encoder.</param>
        public PassthroughAuthenticationHandler(IOptionsMonitor<PassthroughAuthenticationOptions> optionsMonitor, ILoggerFactory logger, UrlEncoder urlEncoder)
            : base(optionsMonitor, logger, urlEncoder)
        {
        }

        /// <summary>
        ///     Authenticates using the configured <see cref="PassthroughAuthenticationOptions.Username"/> and <see cref="PassthroughAuthenticationOptions.Role"/>.
        ///     When AllowRemoteNoAuth is false, only loopback requests are allowed (PR-03).
        /// </summary>
        /// <returns>A successful authentication result, or failure when remote and AllowRemoteNoAuth is false.</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var remote = Context.Connection.RemoteIpAddress;
            var isLoopback = remote != null && IPAddress.IsLoopback(remote);
            if (isLoopback)
                ; // allow
            else if (Options.AllowRemoteNoAuth)
                ; // allow
            else if (!string.IsNullOrWhiteSpace(Options.AllowedCidrs) && remote != null)
            {
                var allowed = false;
                foreach (var cidr in Options.AllowedCidrs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IPAddressRange.TryParse(cidr, out var range) && range.Contains(remote))
                    {
                        allowed = true;
                        break;
                    }
                }
                if (!allowed)
                    return Task.FromResult(AuthenticateResult.Fail("No-auth mode only allowed from loopback, AllowRemoteNoAuth, or AllowedCidrs"));
            }
            else
                return Task.FromResult(AuthenticateResult.Fail("No-auth mode only allowed from loopback"));

            var identity = new GenericIdentity(Options.Username);
            var principal = new GenericPrincipal(identity, new[] { Options.Role.ToString() });
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), PassthroughAuthentication.AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        /// <summary>
        ///     Handles authentication challenges by returning 401. Does not authenticate as a "challenge" (PR-03).
        /// </summary>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Passthrough authentication options.
    /// </summary>
    public class PassthroughAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PassthroughAuthenticationOptions"/> class.
        /// </summary>
        public PassthroughAuthenticationOptions()
        {
        }

        /// <summary>
        ///     Gets or sets the username for the passed-through authentication ticket.
        /// </summary>
        public string Username { get; set; } = "Anonymous";

        /// <summary>
        ///     Gets or sets the role for the passed-through authentication ticket.
        /// </summary>
        public Role Role { get; set; } = Role.Administrator;

        /// <summary>
        ///     When true, allow passthrough from non-loopback addresses. When false, passthrough is loopback-only (PR-03).
        /// </summary>
        public bool AllowRemoteNoAuth { get; set; } = false;

        /// <summary>
        ///     Optional. Comma-separated CIDRs (e.g. 127.0.0.1/32,::1/128) allowed when no-auth in addition to loopback (PR-03).
        /// </summary>
        public string? AllowedCidrs { get; set; }
    }
}
