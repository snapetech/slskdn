// <copyright file="MeshController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.Mesh.API
{
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Mesh.Messages;

    /// <summary>
    ///     Mesh Sync API controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/mesh")]
    [ApiVersion("0")]
    [ApiController]
    public class MeshController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MeshController"/> class.
        /// </summary>
        public MeshController(IMeshSyncService meshSync)
        {
            MeshSync = meshSync;
        }

        private IMeshSyncService MeshSync { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<MeshController>();

        /// <summary>
        ///     Gets mesh sync statistics.
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStats()
        {
            return Ok(MeshSync.Stats);
        }

        /// <summary>
        ///     Gets list of mesh-capable peers.
        /// </summary>
        [HttpGet("peers")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetPeers()
        {
            var peers = MeshSync.GetMeshPeers();
            return Ok(new
            {
                count = peers.Count(),
                peers,
            });
        }

        /// <summary>
        ///     Triggers a sync with a specific peer.
        /// </summary>
        [HttpPost("sync/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> SyncWithPeer(string username)
        {
            var result = await MeshSync.TrySyncWithPeerAsync(username);
            if (!result.Success)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(result);
        }

        /// <summary>
        ///     Generates a HELLO message (for testing/debugging).
        /// </summary>
        [HttpGet("hello")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetHello()
        {
            var hello = MeshSync.GenerateHelloMessage();
            return Ok(hello);
        }

        /// <summary>
        ///     Gets delta entries since a sequence ID.
        /// </summary>
        [HttpGet("delta")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetDelta([FromQuery] long sinceSeq = 0, [FromQuery] int maxEntries = 1000)
        {
            var response = await MeshSync.GenerateDeltaResponseAsync(sinceSeq, maxEntries);
            return Ok(response);
        }

        /// <summary>
        ///     Looks up a specific hash key.
        /// </summary>
        [HttpGet("lookup/{flacKey}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> LookupKey(string flacKey)
        {
            var entry = await MeshSync.LookupHashAsync(flacKey);
            if (entry == null)
            {
                return NotFound(new { flacKey, found = false });
            }

            return Ok(new { flacKey, found = true, entry });
        }

        /// <summary>
        ///     Publishes a hash to the mesh.
        /// </summary>
        [HttpPost("publish")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> PublishHash([FromBody] PublishHashRequest request)
        {
            if (string.IsNullOrEmpty(request?.FlacKey) || string.IsNullOrEmpty(request.ByteHash) || request.Size <= 0)
            {
                return BadRequest(new { error = "flacKey, byteHash, and size are required" });
            }

            await MeshSync.PublishHashAsync(request.FlacKey, request.ByteHash, request.Size, request.MetaFlags);
            return Ok(new { published = true, flacKey = request.FlacKey });
        }

        /// <summary>
        ///     Handles an incoming mesh message (simulates peer-to-peer communication).
        /// </summary>
        [HttpPost("message")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> HandleMessage([FromQuery] string fromUser, [FromBody] JsonElement messageJson)
        {
            if (string.IsNullOrEmpty(fromUser))
            {
                return BadRequest(new { error = "fromUser query parameter required" });
            }

            // Parse message type
            if (!messageJson.TryGetProperty("type", out var typeElement))
            {
                return BadRequest(new { error = "Message must have 'type' property" });
            }

            var messageType = (MeshMessageType)typeElement.GetInt32();
            MeshMessage message = messageType switch
            {
                MeshMessageType.Hello => JsonSerializer.Deserialize<MeshHelloMessage>(messageJson.GetRawText()),
                MeshMessageType.ReqDelta => JsonSerializer.Deserialize<MeshReqDeltaMessage>(messageJson.GetRawText()),
                MeshMessageType.PushDelta => JsonSerializer.Deserialize<MeshPushDeltaMessage>(messageJson.GetRawText()),
                MeshMessageType.ReqKey => JsonSerializer.Deserialize<MeshReqKeyMessage>(messageJson.GetRawText()),
                _ => null,
            };

            if (message == null)
            {
                return BadRequest(new { error = "Unknown or invalid message type" });
            }

            var response = await MeshSync.HandleMessageAsync(fromUser, message);
            if (response == null)
            {
                return Ok(new { handled = true, response = (object)null });
            }

            return Ok(new { handled = true, response });
        }

        /// <summary>
        ///     Merges entries from a peer (simulates receiving PUSH_DELTA).
        /// </summary>
        [HttpPost("merge")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> MergeEntries([FromQuery] string fromUser, [FromBody] MergeEntriesRequest request)
        {
            if (string.IsNullOrEmpty(fromUser))
            {
                return BadRequest(new { error = "fromUser query parameter required" });
            }

            if (request?.Entries == null || !request.Entries.Any())
            {
                return BadRequest(new { error = "entries required" });
            }

            var merged = await MeshSync.MergeEntriesAsync(fromUser, request.Entries);
            return Ok(new
            {
                received = request.Entries.Count(),
                merged,
                stats = MeshSync.Stats,
            });
        }
    }

    /// <summary>
    ///     Request to publish a hash.
    /// </summary>
    public class PublishHashRequest
    {
        /// <summary>Gets or sets the FLAC key.</summary>
        public string FlacKey { get; set; }

        /// <summary>Gets or sets the SHA256 hash of first 32KB.</summary>
        public string ByteHash { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the metadata flags.</summary>
        public int? MetaFlags { get; set; }
    }

    /// <summary>
    ///     Request to merge entries.
    /// </summary>
    public class MergeEntriesRequest
    {
        /// <summary>Gets or sets the entries to merge.</summary>
        public MeshHashEntry[] Entries { get; set; }
    }
}


