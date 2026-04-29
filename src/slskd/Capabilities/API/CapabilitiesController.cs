// <copyright file="CapabilitiesController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Capabilities.API
{
    using System.Linq;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Core.Security;

    /// <summary>
    ///     Capabilities API controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class CapabilitiesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CapabilitiesController"/> class.
        /// </summary>
        /// <param name="capabilityService">The capability service.</param>
        public CapabilitiesController(ICapabilityService capabilityService)
        {
            Capabilities = capabilityService;
        }

        private ICapabilityService Capabilities { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<CapabilitiesController>();

        /// <summary>
        ///     Gets our capabilities.
        /// </summary>
        /// <returns>Our capability information.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetCapabilities()
        {
            return Ok(new
            {
                version = Capabilities.VersionString,
                tag = Capabilities.GetCapabilityTag(),
                json = Capabilities.GetCapabilityFileContent(),
            });
        }

        /// <summary>
        ///     Gets all known slskdn peers.
        /// </summary>
        /// <returns>List of slskdn peers and their capabilities.</returns>
        [HttpGet("peers")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetPeers()
        {
            var peers = Capabilities.GetAllSlskdnPeers()
                .Select(p => new
                {
                    username = p.Username,
                    flags = p.Flags.ToString(),
                    flagsValue = (int)p.Flags,
                    clientVersion = p.ClientVersion,
                    protocolVersion = p.ProtocolVersion,
                    canSwarm = p.CanSwarm,
                    canMeshSync = p.CanMeshSync,
                    lastSeen = p.LastSeen,
                    meshSeqId = p.MeshSeqId,
                });

            return Ok(new
            {
                count = peers.Count(),
                peers,
            });
        }

        /// <summary>
        ///     Gets capabilities for a specific peer.
        /// </summary>
        /// <param name="username">The peer's username.</param>
        /// <returns>Peer capabilities or 404.</returns>
        [HttpGet("peers/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetPeer(string username)
        {
            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required" });
            }

            var caps = Capabilities.GetPeerCapabilities(username);
            if (caps == null)
            {
                return NotFound(new { error = "No capabilities known for peer" });
            }

            return Ok(new
            {
                username = caps.Username,
                flags = caps.Flags.ToString(),
                flagsValue = (int)caps.Flags,
                clientVersion = caps.ClientVersion,
                protocolVersion = caps.ProtocolVersion,
                canSwarm = caps.CanSwarm,
                canMeshSync = caps.CanMeshSync,
                lastSeen = caps.LastSeen,
                meshSeqId = caps.MeshSeqId,
            });
        }

        /// <summary>
        ///     Parses a description string and returns detected capabilities.
        /// </summary>
        /// <param name="request">The parse request.</param>
        /// <returns>Parsed capabilities or null if not slskdn.</returns>
        [HttpPost("parse")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult ParseCapabilities([FromBody] ParseRequest request)
        {
            var description = request?.Description?.Trim();
            var versionString = request?.VersionString?.Trim();
            PeerCapabilities? caps = null;

            if (!string.IsNullOrWhiteSpace(description))
            {
                caps = Capabilities.ParseCapabilityTag(description);
            }

            if (caps == null && !string.IsNullOrWhiteSpace(versionString))
            {
                caps = Capabilities.ParseVersionString(versionString);
            }

            if (caps == null)
            {
                return Ok(new { isSlskdn = false });
            }

            return Ok(new
            {
                isSlskdn = true,
                flags = caps.Flags.ToString(),
                flagsValue = (int)caps.Flags,
                protocolVersion = caps.ProtocolVersion,
                clientVersion = caps.ClientVersion,
                canSwarm = caps.CanSwarm,
                canMeshSync = caps.CanMeshSync,
            });
        }

        /// <summary>
        ///     Gets the mesh-capable peers for sync.
        /// </summary>
        /// <returns>List of mesh-capable peers.</returns>
        [HttpGet("mesh-peers")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetMeshPeers()
        {
            var peers = Capabilities.GetMeshCapablePeers()
                .Select(p => new
                {
                    username = p.Username,
                    lastSeen = p.LastSeen,
                    meshSeqId = p.MeshSeqId,
                });

            return Ok(new
            {
                count = peers.Count(),
                peers,
            });
        }
    }

    /// <summary>
    ///     Request to parse capability strings.
    /// </summary>
    public class ParseRequest
    {
        /// <summary>
        ///     Gets or sets the description string to parse.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the version string to parse.
        /// </summary>
        public string VersionString { get; set; } = string.Empty;
    }
}
