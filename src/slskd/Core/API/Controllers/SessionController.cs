// <copyright file="SessionController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// <copyright file="SessionController.cs" company="slskd Team">
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

namespace slskd.Core.API
{
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Authentication;
    using slskd.Core.Security;

    /// <summary>
    ///     Session.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class SessionController : ControllerBase
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Failures, DateTimeOffset LastFailure, DateTimeOffset? LockoutUntil)> _loginAttempts = new();
        private const int MaxFailures = 10;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(5);

        public SessionController(
            ISecurityService securityService,
            IOptionsSnapshot<Options> optionsSnapshot,
            OptionsAtStartup optionsAtStartup)
        {
            Security = securityService;
            OptionsSnapshot = optionsSnapshot;
            OptionsAtStartup = optionsAtStartup;
        }

        private IOptionsSnapshot<Options> OptionsSnapshot { get; set; }
        private OptionsAtStartup OptionsAtStartup { get; set; }
        private ISecurityService Security { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<SessionController>();

        /// <summary>
        ///     Checks whether the provided authentication is valid.
        /// </summary>
        /// <remarks>This is a no-op provided so that the application can test for an expired token on load.</remarks>
        /// <returns></returns>
        /// <response code="200">The authentication is valid.</response>
        /// <response code="403">The authentication is is invalid.</response>
        [HttpGet]
        [Route("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public IActionResult Check()
        {
            return Ok();
        }

        /// <summary>
        ///     Checks whether security is enabled.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">True if security is enabled, false otherwise.</response>
        [HttpGet]
        [Route("enabled")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(bool), 200)]
        public IActionResult Enabled()
        {
            return Ok(!OptionsAtStartup.Web.Authentication.Disabled);
        }

        /// <summary>
        ///     Logs out, revoking the current JWT.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">Logout successful.</response>
        [HttpDelete]
        [Route("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public IActionResult Logout()
        {
            var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                Security.RevokeToken(jti);
            }

            return NoContent();
        }

        /// <summary>
        ///     Logs in.
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        /// <response code="200">Login was successful.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="401">Login failed.</response>
        [HttpPost]
        [Route("")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TokenResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public IActionResult Login([FromBody] LoginRequest login)
        {
            if (login == default)
            {
                return BadRequest();
            }

            login.Username = login.Username?.Trim() ?? string.Empty;
            login.Password = login.Password?.Trim() ?? string.Empty;

            if (OptionsAtStartup.Headless)
            {
                Log.Warning("Login from {User} rejected; web UI is DISABLED when running in headless mode", login.Username);
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                return BadRequest("Username and/or Password missing or invalid");
            }

            var normalizedUsername = login.Username;
            var normalizedPassword = login.Password;
            var remoteIp = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

            // Check for active lockout
            if (_loginAttempts.TryGetValue(remoteIp, out var existing) && existing.LockoutUntil.HasValue && existing.LockoutUntil.Value > DateTimeOffset.UtcNow)
            {
                Log.Warning("Login from {RemoteIp} rejected; IP is locked out until {LockoutUntil}", remoteIp, existing.LockoutUntil.Value);
                return StatusCode(429, "Too many failed login attempts. Try again later.");
            }

            if (Security.AuthenticateAdminCredentials(normalizedUsername, normalizedPassword))
            {
                // Successful login: clear failed attempt counter
                _loginAttempts.TryRemove(remoteIp, out _);
                return Ok(new TokenResponse(Security.GenerateJwt(normalizedUsername, Role.Administrator)));
            }

            // Failed login: increment counter and potentially lock out
            _loginAttempts.AddOrUpdate(
                remoteIp,
                _ => (1, DateTimeOffset.UtcNow, null),
                (_, prev) =>
                {
                    // Reset window if last failure was outside the window
                    var failures = (DateTimeOffset.UtcNow - prev.LastFailure) > WindowDuration ? 1 : prev.Failures + 1;
                    DateTimeOffset? lockout = failures >= MaxFailures ? DateTimeOffset.UtcNow.Add(LockoutDuration) : null;
                    return (failures, DateTimeOffset.UtcNow, lockout);
                });

            return Unauthorized();
        }
    }
}
