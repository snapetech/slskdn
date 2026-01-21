// <copyright file="BridgeApi.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Bridge;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public interface IBridgeApi
{
    Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default);
    Task<string> DownloadAsync(string username, string filename, string? targetPath, CancellationToken ct = default);
    Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default);
}

public class BridgeSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<BridgeUser> Users { get; set; } = new();
}

public class BridgeUser
{
    public string PeerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<BridgeFile> Files { get; set; } = new();
}

public class BridgeRoom
{
    public string Name { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class BridgeFile
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? MbRecordingId { get; set; }
}

public class BridgeApi : IBridgeApi
{
    private readonly ILogger<BridgeApi> logger;

    public BridgeApi(ILogger<BridgeApi> logger)
    {
        this.logger = logger;
    }

    public Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-BRIDGE] Stub search for query {Query}", query);
        return Task.FromResult(new BridgeSearchResult { Query = query });
    }

    public Task<string> DownloadAsync(string username, string filename, string? targetPath, CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-BRIDGE] Stub download {User}/{File}", username, filename);
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-BRIDGE] Stub get rooms");
        return Task.FromResult(new List<BridgeRoom>());
    }
}
