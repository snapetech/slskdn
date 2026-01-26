// <copyright file="MeshContentMeshService.cs" company="slskd Team">
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

namespace slskd.Mesh.ServiceFabric.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.Transport;
using slskd.Shares;

/// <summary>
///     Mesh service for T-906: Get content by ContentId.
///     Serves file bytes for BackendRef mesh:{peerId}:{contentId} when this node has the content in shares.
/// </summary>
public sealed class MeshContentMeshService : IMeshService
{
    private const int MaxFullResponseBytes = 32 * 1024 * 1024; // 32MB

    private readonly ILogger<MeshContentMeshService> _logger;
    private readonly IShareService _shareService;
    private readonly int _maxPayload;

    public MeshContentMeshService(
        ILogger<MeshContentMeshService> logger,
        IShareService shareService,
        IOptions<MeshOptions>? meshOptions = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        _maxPayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
    }

    public string ServiceName => "MeshContent";

    public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming not implemented for MeshContent");

    public async Task<ServiceReply> HandleCallAsync(ServiceCall call, MeshServiceContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("[MeshContent] {Method} from {PeerId}", call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "GetByContentId" => await HandleGetByContentIdAsync(call, context, cancellationToken),
                _ => new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.MethodNotFound,
                    ErrorMessage = $"Unknown method: {call.Method}",
                    Payload = Array.Empty<byte>(),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MeshContent] Error {Method}", call.Method);
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = ex.Message,
                Payload = Array.Empty<byte>(),
            };
        }
    }

    private async Task<ServiceReply> HandleGetByContentIdAsync(ServiceCall call, MeshServiceContext context, CancellationToken cancellationToken)
    {
        var (req, err) = ServicePayloadParser.TryParseJson<GetByContentIdRequest>(call, _maxPayload);
        if (err != null) return err;
        if (req == null || string.IsNullOrWhiteSpace(req.ContentId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "contentId required",
                Payload = Array.Empty<byte>(),
            };
        }

        var repo = _shareService.GetLocalRepository();
        var ci = repo.FindContentItem(req.ContentId);
        if (ci == null || !ci.Value.IsAdvertisable)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = 404,
                ErrorMessage = "Content not found or not advertisable",
                Payload = Array.Empty<byte>(),
            };
        }

        var finfo = repo.FindFileInfo(ci.Value.MaskedFilename);
        if (finfo.Filename == null || finfo.Size <= 0)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = 404,
                ErrorMessage = "File not found",
                Payload = Array.Empty<byte>(),
            };
        }

        if (!File.Exists(finfo.Filename))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = 404,
                ErrorMessage = "File no longer on disk",
                Payload = Array.Empty<byte>(),
            };
        }

        long offset = 0;
        int length = (int)Math.Min(finfo.Size, int.MaxValue);
        if (req.Range != null)
        {
            offset = Math.Max(0, req.Range.Offset);
            length = req.Range.Length > 0
                ? (int)Math.Min(req.Range.Length, finfo.Size - offset)
                : (int)Math.Max(0, finfo.Size - offset);
        }
        else if (finfo.Size > MaxFullResponseBytes)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.PayloadTooLarge,
                ErrorMessage = $"File too large ({finfo.Size} bytes); use range request (max {MaxFullResponseBytes} without range)",
                Payload = Array.Empty<byte>(),
            };
        }

        byte[] bytes;
        await using (var fs = new FileStream(finfo.Filename, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
            bytes = new byte[length];
            int total = 0;
            while (total < length)
            {
                int r = await fs.ReadAsync(bytes.AsMemory(total, length - total), cancellationToken);
                if (r == 0) break;
                total += r;
            }
            if (total < length)
                Array.Resize(ref bytes, total);
        }

        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = bytes,
        };
    }

    private sealed class GetByContentIdRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("contentId")]
        public string? ContentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("range")]
        public RangeSpec? Range { get; set; }
    }

    private sealed class RangeSpec
    {
        [System.Text.Json.Serialization.JsonPropertyName("offset")]
        public long Offset { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("length")]
        public int Length { get; set; }
    }
}
