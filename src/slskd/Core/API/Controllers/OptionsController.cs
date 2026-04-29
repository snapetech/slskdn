// <copyright file="OptionsController.cs" company="slskd Team">
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

// <copyright file="OptionsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Options;

namespace slskd.Core.API
{
    using System;
    using System.IO;
    using System.Reflection;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Serilog;
    using slskd.Core.Security;
    using slskd.Validation;
    using IOFile = System.IO.File;

    /// <summary>
    ///     Options.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class OptionsController : ControllerBase
    {
        public OptionsController(
            OptionsAtStartup optionsAtStartup,
            IOptionsSnapshot<Options> optionsSnapshot,
            IStateMutator<State> stateMutator)
        {
            OptionsAtStartup = optionsAtStartup;
            OptionsSnapshot = optionsSnapshot;
            StateMutator = stateMutator;
        }

        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private IStateMutator<State> StateMutator { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<OptionsController>();

        /// <summary>
        ///     Gets the current application options.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Options), 200)]
        public IActionResult Current()
        {
            return Ok(OptionsSnapshot.Value.Redact());
        }

        /// <summary>
        ///     Gets the application options provided at startup.
        /// </summary>
        /// <returns></returns>
        /// <summary>
        ///     Overlay the application options with new values.
        /// </summary>
        /// <param name="overlay">The overlay to apply.</param>
        /// <returns></returns>
        [HttpPatch]
        [Authorize(Policy = AuthPolicy.Any, Roles = AuthRole.AdministratorOnly)]
        [ProducesResponseType(typeof(OptionsOverlay), 200)]
        public IActionResult ApplyOverlay([FromBody] OptionsOverlay overlay)
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                Log.Warning("Blocked options overlay application; remote configuration is disabled");
                return Forbid();
            }

            if (overlay is null)
            {
                return BadRequest("Options overlay is required");
            }

            if (!overlay.TryValidate(out var result))
            {
                Log.Warning("Options patch validation failed: {Message}", result.GetResultString());
                return BadRequest("Invalid options overlay");
            }

            try
            {
                Program.ApplyConfigurationOverlay(overlay);
                return Ok(Program.ConfigurationOverlay?.Redact());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply options patch");
                return StatusCode(500, "Failed to apply options patch");
            }
        }

        [HttpGet]
        [Route("startup")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Options), 200)]
        public IActionResult Startup()
        {
            return Ok(OptionsAtStartup.Redact());
        }

        /// <summary>
        ///     Gets the debug view of the current application options.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("debug")]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult Debug()
        {
            if (!OptionsAtStartup.Debug || !OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            // retrieve the IConfigurationRoot instance with reflection to avoid
            // exposing it as a public member of Program.
            var property = typeof(Program).GetProperty("Configuration", BindingFlags.NonPublic | BindingFlags.Static);
            var configurationRoot = property?.GetValue(null, null) as IConfigurationRoot;
            if (configurationRoot is null)
            {
                return StatusCode(500, "Configuration root is unavailable");
            }

            return Ok(configurationRoot.GetDebugView());
        }

        [HttpGet]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        [Route("yaml/location")]
        public IActionResult GetYamlFileLocation()
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            return Ok(Program.ConfigurationFile);
        }

        [HttpGet]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        [Route("yaml")]
        public IActionResult GetYamlFile()
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(Program.ConfigurationFile) || !IOFile.Exists(Program.ConfigurationFile))
            {
                return NotFound();
            }

            var yaml = IOFile.ReadAllText(Program.ConfigurationFile);
            return Ok(yaml);
        }

        [HttpPut]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        [Route("yaml")]
        public IActionResult UpdateYamlFile([FromBody] string yaml)
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            if (yaml is null)
            {
                return BadRequest("YAML is required");
            }

            if (!TryValidateYaml(yaml, out var error))
            {
                Log.Error("Failed to validate YAML configuration: {Error}", error);
                return BadRequest(error);
            }

            try
            {
                IOFile.Copy(Program.ConfigurationFile, $"{Program.ConfigurationFile}.bak", overwrite: true);

                IOFile.WriteAllText(Program.ConfigurationFile, yaml);

                if (OptionsSnapshot.Value.Flags.NoConfigWatch)
                {
                    Log.Information("Configuration watch is disabled; restart required for changes to take effect");
                    StateMutator.SetValue(state => state with { PendingRestart = true });
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update configuration file");
                return StatusCode(500, "Failed to update configuration file");
            }
        }

        [HttpPost]
        [Authorize(Policy = AuthPolicy.Any)]
        [Route("yaml/validate")]
        public IActionResult ValidateYamlFile([FromBody] string yaml)
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            if (yaml is null)
            {
                return BadRequest("YAML is required");
            }

            if (!TryValidateYaml(yaml, out var error))
            {
                return Ok(error);
            }

            return Ok();
        }

        private bool TryValidateYaml(string yaml, out string error)
        {
            error = string.Empty;

            try
            {
                var options = yaml.FromYaml<Options>();

                if (options is null)
                {
                    return true;
                }

                if (!options.TryValidate(out var result))
                {
                    Log.Warning("Configuration validation failed: {Message}", result.GetResultString());
                    error = "Invalid YAML configuration";
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Configuration validation failed");
                error = "Invalid YAML configuration";

                return false;
            }

            return true;
        }
    }
}
