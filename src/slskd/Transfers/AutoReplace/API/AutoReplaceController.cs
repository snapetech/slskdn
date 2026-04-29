// <copyright file="AutoReplaceController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers.AutoReplace.API
{
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Core.Security;

    /// <summary>
    ///     Auto-replace.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class AutoReplaceController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoReplaceController"/> class.
        /// </summary>
        public AutoReplaceController(AutoReplaceBackgroundService autoReplaceBackgroundService)
        {
            BackgroundService = autoReplaceBackgroundService;
        }

        private AutoReplaceBackgroundService BackgroundService { get; }

        /// <summary>
        ///     Gets the current auto-replace status.
        /// </summary>
        /// <returns>The auto-replace status.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStatus()
        {
            return Ok(BackgroundService.GetStatus());
        }

        /// <summary>
        ///     Enables auto-replace.
        /// </summary>
        /// <returns>The updated status.</returns>
        [HttpPut("enable")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult Enable()
        {
            BackgroundService.Enable();
            return Ok(BackgroundService.GetStatus());
        }

        /// <summary>
        ///     Disables auto-replace.
        /// </summary>
        /// <returns>The updated status.</returns>
        [HttpPut("disable")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult Disable()
        {
            BackgroundService.Disable();
            return Ok(BackgroundService.GetStatus());
        }
    }
}
