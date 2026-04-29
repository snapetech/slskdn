// <copyright file="TransfersController.cs" company="slskd Team">
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

// <copyright file="TransfersController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Options;

namespace slskd.Transfers.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Transfers.AutoReplace;
    using slskd.Core.Security;

    /// <summary>
    ///     Transfers.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class TransfersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransfersController"/> class.
        /// </summary>
        /// <param name="optionsSnapshot"></param>
        /// <param name="stateSnapshot"></param>
        /// <param name="transferService"></param>
        /// <param name="autoReplaceService"></param>
        /// <param name="autoReplaceBackgroundService"></param>
        public TransfersController(
            ITransferService transferService,
            IOptionsSnapshot<Options> optionsSnapshot,
            IStateSnapshot<State> stateSnapshot,
            IAutoReplaceService autoReplaceService,
            AutoReplaceBackgroundService autoReplaceBackgroundService)
        {
            Transfers = transferService;
            OptionsSnapshot = optionsSnapshot;
            StateSnapshot = stateSnapshot;
            AutoReplace = autoReplaceService;
            AutoReplaceBackgroundService = autoReplaceBackgroundService;
        }

        private static SemaphoreSlim DownloadRequestLimiter { get; } = new SemaphoreSlim(2, 2);
        private ITransferService Transfers { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private IStateSnapshot<State> StateSnapshot { get; }
        private IAutoReplaceService AutoReplace { get; }
        private AutoReplaceBackgroundService AutoReplaceBackgroundService { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<TransfersController>();

        /// <summary>
        ///     Cancels the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="id">The id of the download.</param>
        /// <param name="remove">A value indicating whether the tracked download should be removed after cancellation.</param>
        /// <param name="deleteFile">A value indicating whether the underlying file should also be deleted.</param>
        /// <returns></returns>
        /// <response code="204">The download was cancelled successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpDelete("downloads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelDownloadAsync([FromRoute, Required] string username, [FromRoute, Required] string id, [FromQuery] bool remove = false, [FromQuery] bool deleteFile = false)
        {
            username = username?.Trim() ?? string.Empty;
            id = id?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || !Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                var download = Transfers.Downloads.Find(t => t.Id == guid);
                if (download == default || !string.Equals(download.Username, username, StringComparison.Ordinal))
                {
                    return NotFound();
                }

                Transfers.Downloads.TryCancel(guid);

                if (remove)
                {
                    Transfers.Downloads.Remove(guid, deleteFile);
                }

                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        ///     Removes all completed downloads, regardless of whether they failed or succeeded.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The downloads were removed successfully.</response>
        [HttpDelete("downloads/all/completed")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public IActionResult ClearCompletedDownloads()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var transfers = Transfers.Downloads.List(t => t.State.HasFlag(Soulseek.TransferStates.Completed));

            foreach (var id in transfers.Select(t => t.Id))
            {
                Transfers.Downloads.Remove(id);
            }

            return NoContent();
        }

        /// <summary>
        ///     Cancels the specified upload.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="id">The id of the upload.</param>
        /// <param name="remove">A value indicating whether the tracked upload should be removed after cancellation.</param>
        /// <returns></returns>
        /// <response code="204">The upload was cancelled successfully.</response>
        /// <response code="404">The specified upload was not found.</response>
        [HttpDelete("uploads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelUpload([FromRoute, Required] string username, [FromRoute, Required] string id, [FromQuery] bool remove = false)
        {
            username = username?.Trim() ?? string.Empty;
            id = id?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || !Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                var upload = Transfers.Uploads.Find(t => t.Id == guid);
                if (upload == default || !string.Equals(upload.Username, username, StringComparison.Ordinal))
                {
                    return NotFound();
                }

                Transfers.Uploads.TryCancel(guid);

                if (remove)
                {
                    Transfers.Uploads.Remove(guid);
                }

                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        ///     Removes all completed uploads, regardless of whether they failed or succeeded.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The uploads were removed successfully.</response>
        [HttpDelete("uploads/all/completed")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public IActionResult ClearCompletedUploads()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            // get all the transfers that aren't removed
            var transfers = Transfers.Uploads
                .List(t => t.State.HasFlag(Soulseek.TransferStates.Completed), includeRemoved: false);

            foreach (var id in transfers.Select(t => t.Id))
            {
                Transfers.Uploads.Remove(id);
            }

            return NoContent();
        }

        /// <summary>
        ///     Gets upload diagnostics for listener, share, and recent upload state.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/diagnostics")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(UploadDiagnosticsResponse), 200)]
        public async Task<IActionResult> GetUploadDiagnostics(CancellationToken cancellationToken = default)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var options = OptionsSnapshot.Value;
            var state = StateSnapshot.Value;
            var uploads = Transfers.Uploads.List(t => true, includeRemoved: true);
            var recentUploads = uploads
                .OrderByDescending(t => t.EnqueuedAt ?? t.StartedAt ?? t.EndedAt ?? t.RequestedAt)
                .Take(25)
                .Select(t => new UploadDiagnosticsResponse.RecentUpload
                {
                    Id = t.Id,
                    Username = t.Username,
                    Filename = t.Filename,
                    State = t.State.ToString(),
                    RequestedAt = t.RequestedAt,
                    EnqueuedAt = t.EnqueuedAt,
                    StartedAt = t.StartedAt,
                    EndedAt = t.EndedAt,
                    BytesTransferred = t.BytesTransferred,
                    Size = t.Size,
                    Exception = t.Exception,
                    Removed = t.Removed,
                })
                .ToList();

            var listenProbe = await ProbeConfiguredListenPortAsync(
                options.Soulseek.ListenIpAddress,
                options.Soulseek.ListenPort,
                cancellationToken);

            var warnings = new List<string>();

            if (state.Server.IsConnected && state.Server.IsLoggedIn && !listenProbe.Succeeded)
            {
                warnings.Add("Local TCP probe could not connect to the configured Soulseek listener; uploads from remote peers will probably fail until the listener is reachable.");
            }

            if (IPAddress.TryParse(options.Soulseek.ListenIpAddress, out var listenAddress) && IPAddress.IsLoopback(listenAddress))
            {
                warnings.Add("Soulseek listen address is loopback; remote peers cannot connect to 127.0.0.1/::1 from outside this host.");
            }

            if (state.Shares.Files == 0)
            {
                warnings.Add("No shared files are currently indexed; remote requests may be rejected as File not shared.");
            }

            if (state.Shares.ScanPending || state.Shares.Scanning)
            {
                warnings.Add("Share scan is pending or running; upload availability may not match the filesystem until scanning completes.");
            }

            if (uploads.Count == 0)
            {
                warnings.Add("No upload records are present. If a remote user attempted an upload, this usually means the request did not reach slskdN or failed before enqueue.");
            }

            return Ok(new UploadDiagnosticsResponse
            {
                GeneratedAt = DateTime.UtcNow,
                SoulseekState = state.Server.State.ToString(),
                IsConnected = state.Server.IsConnected,
                IsLoggedIn = state.Server.IsLoggedIn,
                ListenIpAddress = options.Soulseek.ListenIpAddress,
                ListenPort = options.Soulseek.ListenPort,
                LocalListenProbe = listenProbe,
                UploadSlots = options.Global.Upload.Slots,
                UploadSpeedLimit = options.Global.Upload.SpeedLimit,
                ShareDirectories = state.Shares.Directories,
                ShareFiles = state.Shares.Files,
                ShareScanPending = state.Shares.ScanPending,
                ShareScanning = state.Shares.Scanning,
                TotalUploadRecords = uploads.Count,
                ActiveUploads = uploads.Count(t => !t.State.HasFlag(Soulseek.TransferStates.Completed)),
                FailedUploads = uploads.Count(t => t.State.HasFlag(Soulseek.TransferStates.Completed) && !t.State.HasFlag(Soulseek.TransferStates.Succeeded)),
                SucceededUploads = uploads.Count(t => t.State.HasFlag(Soulseek.TransferStates.Completed | Soulseek.TransferStates.Succeeded)),
                RecentUploads = recentUploads,
                Warnings = warnings,
            });
        }

        /// <summary>
        ///     Enqueues the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="requests">The list of download requests.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> EnqueueAsync([FromRoute, Required] string username, [FromBody, Required] IEnumerable<QueueDownloadRequest> requests)
        {
            username = username?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetReadableString());
            }

            var requestList = requests?.ToList();

            if (requestList == null || !requestList.Any())
            {
                return BadRequest("At least one file is required");
            }

            if (requestList.Any(r => r is null))
            {
                return BadRequest("One or more records in the request are null");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            var normalizedRequests = requestList
                .Select(r => new QueueDownloadRequest
                {
                    Filename = r!.Filename?.Trim() ?? string.Empty,
                    Size = r.Size,
                    BatchId = r.BatchId,
                })
                .ToList();

            if (normalizedRequests.Any(r => string.IsNullOrWhiteSpace(r.Filename)))
            {
                return BadRequest("Each file requires a non-empty filename");
            }

            if (!DownloadRequestLimiter.Wait(0))
            {
                return StatusCode(429, "Only one concurrent operation is permitted. Wait until the previous request completes");
            }

            try
            {
                var batchId = normalizedRequests.Count > 1
                    ? normalizedRequests.FirstOrDefault(r => r.BatchId.HasValue)?.BatchId ?? Guid.NewGuid()
                    : normalizedRequests.FirstOrDefault()?.BatchId;

                var (enqueued, failed) = await Transfers.Downloads.EnqueueAsync(
                    username,
                    normalizedRequests.Select(r => (r.Filename, r.Size, r.BatchId ?? batchId)));

                return StatusCode(201, new { Enqueued = enqueued, Failed = failed });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files for {Username}", requestList.Count, username);
                return StatusCode(500, "Failed to enqueue downloads");
            }
            finally
            {
                DownloadRequestLimiter.Release();
            }
        }

        /// <summary>
        ///     Gets all downloads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetDownloadsAsync([FromQuery] bool includeRemoved = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var downloads = Transfers.Downloads.List(includeRemoved: includeRemoved);

            var response = downloads.GroupBy(t => t.Username).Select(grouping => new UserResponse()
            {
                Username = grouping.Key,
                Directories = grouping.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            });

            return Ok(response);
        }

        /// <summary>
        ///     Gets all downloads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetDownloadsAsync([FromRoute, Required] string username)
        {
            username = username?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest();
            }

            var downloads = Transfers.Downloads.List(d => d.Username == username);

            if (!downloads.Any())
            {
                return NotFound();
            }

            var response = new UserResponse()
            {
                Username = username,
                Directories = downloads.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            };

            return Ok(response);
        }

        [HttpGet("downloads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(API.Transfer), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetDownload([FromRoute, Required] string username, [FromRoute, Required] string id)
        {
            username = username?.Trim() ?? string.Empty;
            id = id?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || !Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            var download = Transfers.Downloads.Find(t => t.Id == guid);

            if (download == default || !string.Equals(download.Username, username, StringComparison.Ordinal))
            {
                return NotFound();
            }

            return Ok(download);
        }

        /// <summary>
        ///     Gets the download for the specified username matching the specified filename, and requests
        ///     the current place in the remote queue of the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="id">The id of the download.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpGet("downloads/{username}/{id}/position")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(API.Transfer), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPlaceInQueueAsync([FromRoute, Required] string username, [FromRoute, Required] string id)
        {
            username = username?.Trim() ?? string.Empty;
            id = id?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || !Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                var download = Transfers.Downloads.Find(t => t.Id == guid);
                if (download == default || !string.Equals(download.Username, username, StringComparison.Ordinal))
                {
                    return NotFound();
                }

                var place = await Transfers.Downloads.GetPlaceInQueueAsync(guid);
                return Ok(place);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get place in queue for {Username}/{TransferId}", username, guid);
                return StatusCode(500, "Failed to get queue position");
            }
        }

        /// <summary>
        ///     Gets real-time transfer speeds (total, soulseek, mesh).
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("speeds")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetSpeeds()
        {
            // Calculate total speeds from active transfers
            var activeDownloads = Transfers.Downloads.List(t =>
                t.State == Soulseek.TransferStates.InProgress);
            var activeUploads = Transfers.Uploads.List(t =>
                t.State == Soulseek.TransferStates.InProgress, includeRemoved: false);

            var totalDownloadSpeed = activeDownloads.Sum(t => t.AverageSpeed);
            var totalUploadSpeed = activeUploads.Sum(t => t.AverageSpeed);
            var totalSpeed = totalDownloadSpeed + totalUploadSpeed;

            // TODO: Distinguish mesh vs soulseek transfers
            // For now, all transfers are soulseek-based
            var soulseekSpeed = totalSpeed;
            var meshSpeed = 0.0;

            return Ok(new
            {
                total = totalSpeed,
                soulseek = soulseekSpeed,
                mesh = meshSpeed,
                download = totalDownloadSpeed,
                upload = totalUploadSpeed
            });
        }

        /// <summary>
        ///     Gets all uploads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromQuery] bool includeRemoved = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            // todo: refactor this so it doesn't return the world. start and end time params
            // should be required.  consider pagination.
            var uploads = Transfers.Uploads.List(t => true, includeRemoved: includeRemoved);

            var response = uploads.GroupBy(t => t.Username).Select(grouping => new UserResponse()
            {
                Username = grouping.Key,
                Directories = grouping.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            });

            return Ok(response);
        }

        private static async Task<UploadDiagnosticsResponse.ListenProbe> ProbeConfiguredListenPortAsync(
            string listenIpAddress,
            int listenPort,
            CancellationToken cancellationToken)
        {
            var target = ResolveLocalProbeAddress(listenIpAddress);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(750));

            try
            {
                using var client = new TcpClient(target.AddressFamily);
                var started = DateTime.UtcNow;
                await client.ConnectAsync(target, listenPort, timeout.Token);

                return new UploadDiagnosticsResponse.ListenProbe
                {
                    TargetIpAddress = target.ToString(),
                    Port = listenPort,
                    Succeeded = true,
                    LatencyMilliseconds = (DateTime.UtcNow - started).TotalMilliseconds,
                };
            }
            catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException || ex is TimeoutException)
            {
                return new UploadDiagnosticsResponse.ListenProbe
                {
                    TargetIpAddress = target.ToString(),
                    Port = listenPort,
                    Succeeded = false,
                    Error = ex.Message,
                };
            }
        }

        private static IPAddress ResolveLocalProbeAddress(string listenIpAddress)
        {
            if (!IPAddress.TryParse(listenIpAddress, out var address))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.Any.Equals(address))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.IPv6Any.Equals(address))
            {
                return IPAddress.IPv6Loopback;
            }

            return address;
        }

        /// <summary>
        ///     Gets all uploads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required] string username)
        {
            username = username?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest();
            }

            var uploads = Transfers.Uploads.List(d => d.Username == username, includeRemoved: false);

            if (!uploads.Any())
            {
                return NotFound();
            }

            var response = new UserResponse()
            {
                Username = username,
                Directories = uploads.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            };

            return Ok(response);
        }

        /// <summary>
        ///     Gets the upload for the specified username matching the specified filename.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="id">The id of the upload.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required] string username, [FromRoute, Required] string id)
        {
            username = username?.Trim() ?? string.Empty;
            id = id?.Trim() ?? string.Empty;

            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || !Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            var upload = Transfers.Uploads.Find(t => t.Id == guid);

            if (upload == default || !string.Equals(upload.Username, username, StringComparison.Ordinal))
            {
                return NotFound();
            }

            return Ok(upload);
        }

        /// <summary>
        ///     Gets all stuck downloads (candidates for auto-replacement).
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/stuck")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetStuckDownloads()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var stuckDownloads = AutoReplace.GetStuckDownloads();
            return Ok(stuckDownloads);
        }

        /// <summary>
        ///     Finds alternative sources for a stuck download.
        /// </summary>
        /// <param name="request">The find alternative request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        /// <response code="200">Alternatives were found successfully.</response>
        [HttpPost("downloads/find-alternative")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> FindAlternativeAsync(
            [FromBody] FindAlternativeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest();
            }

            var alternatives = await AutoReplace.FindAlternativesAsync(request, cancellationToken);
            return Ok(alternatives);
        }

        /// <summary>
        ///     Replaces a stuck download with an alternative source.
        /// </summary>
        /// <param name="request">The replace download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        /// <response code="200">The download was replaced successfully.</response>
        /// <response code="500">Failed to replace the download.</response>
        [HttpPost("downloads/replace")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ReplaceDownloadAsync(
            [FromBody] ReplaceDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest();
            }

            var success = await AutoReplace.ReplaceDownloadAsync(request, cancellationToken);
            if (success)
            {
                return Ok(new { success = true });
            }

            return StatusCode(500, new { success = false, error = "Failed to replace download" });
        }

        /// <summary>
        ///     Processes all stuck downloads and attempts auto-replacement.
        /// </summary>
        /// <param name="request">The auto-replace request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        /// <response code="200">The auto-replace process completed.</response>
        [HttpPost("downloads/auto-replace")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(AutoReplaceResult), 200)]
        public async Task<IActionResult> AutoReplaceAsync(
            [FromBody] AutoReplaceRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest();
            }

            var result = await AutoReplace.ProcessStuckDownloadsAsync(request, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        ///     Gets the auto-replace status.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/auto-replace/status")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetAutoReplaceStatus()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var stuckCount = AutoReplace.GetStuckDownloads().Count();
            var status = AutoReplaceBackgroundService.GetStatus();
            return Ok(new { stuckCount, enabled = status.Enabled, intervalSeconds = status.IntervalSeconds });
        }

        /// <summary>
        ///     Gets download history statistics grouped by username.
        /// </summary>
        /// <returns>Dictionary of usernames to download statistics.</returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/user-stats")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, UserDownloadStats>), 200)]
        public IActionResult GetUserDownloadStats()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var downloads = Transfers.Downloads.List();

            var stats = downloads
                .GroupBy(d => d.Username)
                .ToDictionary(
                    g => g.Key,
                    g => new UserDownloadStats
                    {
                        Username = g.Key,
                        TotalDownloads = g.Count(),
                        SuccessfulDownloads = g.Count(d => d.State.HasFlag(Soulseek.TransferStates.Completed) && d.State.HasFlag(Soulseek.TransferStates.Succeeded)),
                        FailedDownloads = g.Count(d => d.State.HasFlag(Soulseek.TransferStates.Completed) && !d.State.HasFlag(Soulseek.TransferStates.Succeeded)),
                        TotalBytes = g.Where(d => d.State.HasFlag(Soulseek.TransferStates.Succeeded)).Sum(d => d.BytesTransferred),
                        LastDownloadAt = g.Max(d => d.EndedAt),
                    });

            return Ok(stats);
        }
    }

    /// <summary>
    ///     Download statistics for a user.
    /// </summary>
    public class UserDownloadStats
    {
        public string Username { get; set; } = string.Empty;
        public int TotalDownloads { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public long TotalBytes { get; set; }
        public DateTime? LastDownloadAt { get; set; }
    }

    public class UploadDiagnosticsResponse
    {
        public DateTime GeneratedAt { get; set; }
        public string SoulseekState { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public bool IsLoggedIn { get; set; }
        public string ListenIpAddress { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public ListenProbe LocalListenProbe { get; set; } = new();
        public int UploadSlots { get; set; }
        public int UploadSpeedLimit { get; set; }
        public int ShareDirectories { get; set; }
        public int ShareFiles { get; set; }
        public bool ShareScanPending { get; set; }
        public bool ShareScanning { get; set; }
        public int TotalUploadRecords { get; set; }
        public int ActiveUploads { get; set; }
        public int FailedUploads { get; set; }
        public int SucceededUploads { get; set; }
        public IReadOnlyList<RecentUpload> RecentUploads { get; set; } = Array.Empty<RecentUpload>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

        public class ListenProbe
        {
            public string TargetIpAddress { get; set; } = string.Empty;
            public int Port { get; set; }
            public bool Succeeded { get; set; }
            public double? LatencyMilliseconds { get; set; }
            public string? Error { get; set; }
        }

        public class RecentUpload
        {
            public Guid Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Filename { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public DateTime? RequestedAt { get; set; }
            public DateTime? EnqueuedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? EndedAt { get; set; }
            public long BytesTransferred { get; set; }
            public long Size { get; set; }
            public string? Exception { get; set; }
            public bool Removed { get; set; }
        }
    }
}
