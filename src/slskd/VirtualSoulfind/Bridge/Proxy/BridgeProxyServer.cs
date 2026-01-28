// <copyright file="BridgeProxyServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Bridge.Proxy;

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd;
using slskd.VirtualSoulfind.Bridge;
using slskd.VirtualSoulfind.Bridge.Protocol;
using OptionsModel = slskd.Options;

/// <summary>
/// TCP server that accepts Soulseek protocol connections and proxies to bridge API.
/// Alternative to forking Soulfind - implements minimal Soulseek server protocol.
/// </summary>
public class BridgeProxyServer : BackgroundService
{
    private readonly ILogger<BridgeProxyServer> logger;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly IBridgeApi bridgeApi;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly SoulseekProtocolParser protocolParser;
    private readonly ITransferProgressProxy progressProxy;
    private TcpListener? listener;
    private readonly ConcurrentDictionary<string, ClientSession> activeSessions = new();
    private readonly ConcurrentDictionary<string, string> clientIdToProxyId = new(); // Map client ID to transfer proxy ID

    public BridgeProxyServer(
        ILogger<BridgeProxyServer> logger,
        IOptionsMonitor<OptionsModel> optionsMonitor,
        IBridgeApi bridgeApi,
        IHttpClientFactory httpClientFactory,
        SoulseekProtocolParser protocolParser,
        ITransferProgressProxy progressProxy)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
        this.bridgeApi = bridgeApi;
        this.httpClientFactory = httpClientFactory;
        this.protocolParser = protocolParser;
        this.progressProxy = progressProxy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        // This yields immediately so Kestrel can start binding while bridge proxy initializes
        await Task.Yield();

        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Bridge?.Enabled != true)
        {
            logger.LogInformation("[VSF-BRIDGE-PROXY] Bridge disabled, proxy server not starting");
            return;
        }

        var port = options.VirtualSoulfind.Bridge.Port > 0
            ? options.VirtualSoulfind.Bridge.Port
            : 2242;

        var maxClients = options.VirtualSoulfind.Bridge.MaxClients > 0
            ? options.VirtualSoulfind.Bridge.MaxClients
            : 10;

        logger.LogInformation("[VSF-BRIDGE-PROXY] Starting bridge proxy server on port {Port} (max {MaxClients} clients)",
            port, maxClients);

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            logger.LogInformation("[VSF-BRIDGE-PROXY] Bridge proxy server listening on port {Port}", port);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check client limit
                    if (activeSessions.Count >= maxClients)
                    {
                        logger.LogWarning("[VSF-BRIDGE-PROXY] Max clients ({MaxClients}) reached, rejecting new connection",
                            maxClients);
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    var client = await listener.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString("N");
                    
                    logger.LogInformation("[VSF-BRIDGE-PROXY] New client connection: {ClientId} from {Endpoint}",
                        clientId, client.Client.RemoteEndPoint);

                    // Handle client in background task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(clientId, client, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error handling client {ClientId}", clientId);
                        }
                        finally
                        {
                            activeSessions.TryRemove(clientId, out _);
                            client?.Close();
                        }
                    }, stoppingToken);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error accepting client connection");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Failed to start proxy server: {Message}", ex.Message);
        }
        finally
        {
            listener?.Stop();
            logger.LogInformation("[VSF-BRIDGE-PROXY] Bridge proxy server stopped");
        }
    }

    private async Task HandleClientAsync(string clientId, TcpClient client, CancellationToken ct)
    {
        var session = new ClientSession
        {
            ClientId = clientId,
            TcpClient = client,
            ConnectedAt = DateTimeOffset.UtcNow
        };
        activeSessions[clientId] = session;

        var stream = client.GetStream();

            try
            {
                // Phase 1: Handshake
                await PerformHandshakeAsync(stream, session, ct);

                // Phase 2: Login
                var loginResult = await PerformLoginAsync(stream, session, ct);
                if (!loginResult)
                {
                    logger.LogWarning("[VSF-BRIDGE-PROXY] Client {ClientId} login failed", clientId);
                    return;
                }

                // Phase 3: Handle requests (T-851.8: Error handling and graceful degradation)
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        // Read message using protocol parser
                        var message = await protocolParser.ReadMessageAsync(stream, ct);
                        if (message == null)
                        {
                            // Connection closed
                            break;
                        }

                        // Route to appropriate handler
                        session.RequestCount++;
                        switch (message.Type)
                        {
                            case SoulseekProtocolParser.MessageType.SearchRequest:
                                await HandleSearchRequestAsync(stream, message, session, ct);
                                break;

                            case SoulseekProtocolParser.MessageType.DownloadRequest:
                                await HandleDownloadRequestAsync(stream, message, session, ct);
                                break;

                            case SoulseekProtocolParser.MessageType.RoomListRequest:
                                await HandleRoomListRequestAsync(stream, message, session, ct);
                                break;

                            default:
                                logger.LogWarning("[VSF-BRIDGE-PROXY] Unhandled message type: {Type} from {ClientId}",
                                    message.Type, session.ClientId);
                                // Send error response for unknown message types
                                await SendErrorResponseAsync(stream, "Unknown message type", ct);
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown requested
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        // Client disconnected
                        logger.LogDebug("[VSF-BRIDGE-PROXY] Client {ClientId} disconnected: {Message}",
                            clientId, ioEx.Message);
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing (graceful degradation)
                        logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error handling message from {ClientId}: {Message}",
                            clientId, ex.Message);
                        
                        // Try to send error response
                        try
                        {
                            await SendErrorResponseAsync(stream, $"Internal error: {ex.Message}", ct);
                        }
                        catch
                        {
                            // Failed to send error - client likely disconnected
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VSF-BRIDGE-PROXY] Fatal error in client session {ClientId}: {Message}",
                    clientId, ex.Message);
            }
            finally
            {
                // Cleanup (T-851.6)
                if (session.ActiveProxyId != null)
                {
                    try
                    {
                        await progressProxy.StopProxyAsync(session.ActiveProxyId, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[VSF-BRIDGE-PROXY] Error stopping proxy for {ClientId}", clientId);
                    }
                }
                clientIdToProxyId.TryRemove(clientId, out _);
            }
    }

    private async Task PerformHandshakeAsync(NetworkStream stream, ClientSession session, CancellationToken ct)
    {
        // Soulseek handshake: client sends version string
        // For now, we'll skip detailed handshake and proceed to login
        logger.LogDebug("[VSF-BRIDGE-PROXY] Performing handshake for {ClientId}", session.ClientId);
        await Task.CompletedTask;
    }

    private async Task<bool> PerformLoginAsync(NetworkStream stream, ClientSession session, CancellationToken ct)
    {
        try
        {
            // Read login request
            var message = await protocolParser.ReadMessageAsync(stream, ct);
            if (message == null || message.Type != SoulseekProtocolParser.MessageType.Login)
            {
                logger.LogWarning("[VSF-BRIDGE-PROXY] Expected login message, got {Type}", message?.Type);
                return false;
            }

            var loginRequest = protocolParser.ParseLoginRequest(message.Payload);
            if (loginRequest == null)
            {
                logger.LogWarning("[VSF-BRIDGE-PROXY] Failed to parse login request");
                return false;
            }

            // Validate authentication if required (T-851.7)
            var options = optionsMonitor.CurrentValue;
            if (options.VirtualSoulfind.Bridge.RequireAuth)
            {
                var configuredPassword = options.VirtualSoulfind.Bridge.Password;
                if (string.IsNullOrWhiteSpace(configuredPassword))
                {
                    logger.LogWarning("[VSF-BRIDGE-PROXY] RequireAuth is true but no password configured");
                    var errorPayload = protocolParser.BuildLoginResponse(false, "Server authentication misconfigured");
                    await protocolParser.WriteMessageAsync(
                        stream,
                        SoulseekProtocolParser.MessageType.LoginResponse,
                        errorPayload,
                        ct);
                    return false;
                }

                if (loginRequest.Password != configuredPassword)
                {
                    logger.LogWarning("[VSF-BRIDGE-PROXY] Authentication failed for {Username} from {ClientId}",
                        loginRequest.Username, session.ClientId);
                    var errorPayload = protocolParser.BuildLoginResponse(false, "Invalid username or password");
                    await protocolParser.WriteMessageAsync(
                        stream,
                        SoulseekProtocolParser.MessageType.LoginResponse,
                        errorPayload,
                        ct);
                    return false;
                }

                logger.LogDebug("[VSF-BRIDGE-PROXY] Authentication successful for {Username}", loginRequest.Username);
            }

            session.Username = loginRequest.Username;
            session.IsAuthenticated = true;

            // Send login response (success)
            var responsePayload = protocolParser.BuildLoginResponse(true, "Login successful");
            await protocolParser.WriteMessageAsync(
                stream,
                SoulseekProtocolParser.MessageType.LoginResponse,
                responsePayload,
                ct);

            logger.LogInformation("[VSF-BRIDGE-PROXY] Client {ClientId} logged in as {Username}",
                session.ClientId, session.Username);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error during login for {ClientId}", session.ClientId);
            return false;
        }
    }

    private async Task HandleSearchRequestAsync(
        NetworkStream stream,
        SoulseekMessage message,
        ClientSession session,
        CancellationToken ct)
    {
        var searchRequest = protocolParser.ParseSearchRequest(message.Payload);
        if (searchRequest == null)
        {
            logger.LogWarning("[VSF-BRIDGE-PROXY] Failed to parse search request from {ClientId}", session.ClientId);
            return;
        }

        logger.LogInformation("[VSF-BRIDGE-PROXY] Search request from {ClientId}: {Query} (token: {Token})",
            session.ClientId, searchRequest.Query, searchRequest.Token);

        try
        {
            // Call bridge API
            var bridgeResult = await bridgeApi.SearchAsync(searchRequest.Query, ct);

            // Convert bridge results to Soulseek format
            var searchFiles = new List<SearchFileResult>();
            foreach (var user in bridgeResult.Users)
            {
                foreach (var file in user.Files)
                {
                    searchFiles.Add(new SearchFileResult
                    {
                        Username = user.Username,
                        Filename = file.Path,
                        Size = file.SizeBytes,
                        Code = 0, // File code (0 = normal file)
                        Extension = System.IO.Path.GetExtension(file.Path)
                    });
                }
            }

            // Build and send response
            var responsePayload = protocolParser.BuildSearchResponse(searchRequest.Token, searchFiles);
            await protocolParser.WriteMessageAsync(
                stream,
                SoulseekProtocolParser.MessageType.SearchResponse,
                responsePayload,
                ct);

            logger.LogDebug("[VSF-BRIDGE-PROXY] Sent {Count} search results to {ClientId}",
                searchFiles.Count, session.ClientId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error processing search request from {ClientId}", session.ClientId);
        }
    }

    private async Task HandleDownloadRequestAsync(
        NetworkStream stream,
        SoulseekMessage message,
        ClientSession session,
        CancellationToken ct)
    {
        var downloadRequest = protocolParser.ParseDownloadRequest(message.Payload);
        if (downloadRequest == null)
        {
            logger.LogWarning("[VSF-BRIDGE-PROXY] Failed to parse download request from {ClientId}", session.ClientId);
            return;
        }

        logger.LogInformation("[VSF-BRIDGE-PROXY] Download request from {ClientId}: {Username}/{Filename} (token: {Token})",
            session.ClientId, downloadRequest.Username, downloadRequest.Filename, downloadRequest.Token);

        try
        {
            // Call bridge API to start download
            var transferId = await bridgeApi.DownloadAsync(
                downloadRequest.Username,
                downloadRequest.Filename,
                null, // Target path will be determined by bridge
                ct);

            // Start progress proxy for this client (T-851.5)
            var proxyId = await progressProxy.StartProxyAsync(transferId, session.ClientId, ct);
            clientIdToProxyId[session.ClientId] = proxyId;
            session.ActiveTransferId = transferId;
            session.ActiveProxyId = proxyId;

            // Send download response (accept)
            // Format: [success: bool] [transfer_id: string] [token: int32]
            using var responseStream = new MemoryStream();
            using var writer = new BinaryWriter(responseStream);
            writer.Write(true); // Success
            var transferIdBytes = Encoding.UTF8.GetBytes(transferId);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            writer.Write(downloadRequest.Token);
            var responsePayload = responseStream.ToArray();

            await protocolParser.WriteMessageAsync(
                stream,
                SoulseekProtocolParser.MessageType.DownloadResponse,
                responsePayload,
                ct);

            logger.LogInformation("[VSF-BRIDGE-PROXY] Started download {TransferId} (proxy {ProxyId}) for {ClientId}",
                transferId, proxyId, session.ClientId);

            // Start background task to push progress updates (T-851.5)
            _ = Task.Run(async () => await PushProgressUpdatesAsync(session, ct), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error processing download request from {ClientId}", session.ClientId);
            
            // Send error response
            try
            {
                using var errorStream = new MemoryStream();
                using var writer = new BinaryWriter(errorStream);
                writer.Write(false); // Failure
                var errorMsg = Encoding.UTF8.GetBytes(ex.Message);
                writer.Write(errorMsg.Length);
                writer.Write(errorMsg);
                writer.Write(downloadRequest.Token);
                var errorPayload = errorStream.ToArray();
                
                await protocolParser.WriteMessageAsync(
                    stream,
                    SoulseekProtocolParser.MessageType.DownloadResponse,
                    errorPayload,
                    ct);
            }
            catch (Exception sendEx)
            {
                logger.LogError(sendEx, "[VSF-BRIDGE-PROXY] Failed to send error response to {ClientId}", session.ClientId);
            }
        }
    }

    private async Task HandleRoomListRequestAsync(
        NetworkStream stream,
        SoulseekMessage message,
        ClientSession session,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-BRIDGE-PROXY] Room list request from {ClientId}", session.ClientId);

        try
        {
            // Call bridge API
            var bridgeRooms = await bridgeApi.GetRoomsAsync(ct);

            // Convert to Soulseek format
            var rooms = bridgeRooms.Select(r => new RoomInfo
            {
                Name = r.Name,
                UserCount = r.MemberCount
            }).ToList();

            // Build and send response
            var responsePayload = protocolParser.BuildRoomListResponse(rooms);
            await protocolParser.WriteMessageAsync(
                stream,
                SoulseekProtocolParser.MessageType.RoomListResponse,
                responsePayload,
                ct);

            logger.LogDebug("[VSF-BRIDGE-PROXY] Sent {Count} rooms to {ClientId}", rooms.Count, session.ClientId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error processing room list request from {ClientId}", session.ClientId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[VSF-BRIDGE-PROXY] Stopping bridge proxy server");

        // Stop all progress proxies (T-851.6)
        foreach (var kvp in clientIdToProxyId)
        {
            try
            {
                await progressProxy.StopProxyAsync(kvp.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-BRIDGE-PROXY] Error stopping proxy {ProxyId} for client {ClientId}",
                    kvp.Value, kvp.Key);
            }
        }
        clientIdToProxyId.Clear();

        // Close all client connections (T-851.6)
        foreach (var session in activeSessions.Values)
        {
            try
            {
                if (session.ActiveProxyId != null)
                {
                    await progressProxy.StopProxyAsync(session.ActiveProxyId, cancellationToken);
                }
                session.TcpClient?.Close();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-BRIDGE-PROXY] Error closing client {ClientId}", session.ClientId);
            }
        }

        activeSessions.Clear();
        listener?.Stop();

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// T-851.5: Push transfer progress updates to legacy client.
    /// </summary>
    private async Task PushProgressUpdatesAsync(ClientSession session, CancellationToken ct)
    {
        if (session.ActiveProxyId == null)
        {
            return;
        }

        try
        {
            var lastPercent = -1;
            while (!ct.IsCancellationRequested && session.TcpClient?.Connected == true)
            {
                // Get current progress
                var progress = await progressProxy.GetLegacyProgressAsync(session.ActiveProxyId, ct);
                if (progress == null)
                {
                    // Transfer completed or failed
                    break;
                }

                // Only send update if percent changed significantly (avoid spam)
                if (progress.PercentComplete != lastPercent && progress.PercentComplete % 5 == 0)
                {
                    try
                    {
                        // Build progress update message
                        using var updateStream = new MemoryStream();
                        using var writer = new BinaryWriter(updateStream);
                        writer.Write(progress.BytesTransferred);
                        writer.Write(progress.FileSize);
                        writer.Write(progress.PercentComplete);
                        writer.Write(progress.AverageSpeed);
                        var stateBytes = Encoding.UTF8.GetBytes(progress.State);
                        writer.Write(stateBytes.Length);
                        writer.Write(stateBytes);
                        var updatePayload = updateStream.ToArray();

                        // Send progress update (using FileTransfer message type)
                        await protocolParser.WriteMessageAsync(
                            session.TcpClient.GetStream(),
                            SoulseekProtocolParser.MessageType.FileTransfer,
                            updatePayload,
                            ct);

                        lastPercent = progress.PercentComplete;
                        logger.LogDebug("[VSF-BRIDGE-PROXY] Progress update for {ClientId}: {Percent}%",
                            session.ClientId, progress.PercentComplete);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[VSF-BRIDGE-PROXY] Error sending progress update to {ClientId}",
                            session.ClientId);
                        break; // Client disconnected
                    }
                }

                // Wait before next check
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE-PROXY] Error in progress update loop for {ClientId}", session.ClientId);
        }
    }

    /// <summary>
    /// T-851.8: Send error response to client.
    /// </summary>
    private async Task SendErrorResponseAsync(NetworkStream stream, string errorMessage, CancellationToken ct)
    {
        try
        {
            // Build error response (using generic error message format)
            using var errorStream = new MemoryStream();
            using var writer = new BinaryWriter(errorStream);
            var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
            writer.Write(errorBytes.Length);
            writer.Write(errorBytes);
            var errorPayload = errorStream.ToArray();

            // Use a generic error message type (or reuse LoginResponse with success=false)
            await protocolParser.WriteMessageAsync(
                stream,
                SoulseekProtocolParser.MessageType.LoginResponse, // Reuse for error
                errorPayload,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-BRIDGE-PROXY] Failed to send error response: {Message}", errorMessage);
        }
    }

    private class ClientSession
    {
        public string ClientId { get; set; } = string.Empty;
        public TcpClient TcpClient { get; set; } = null!;
        public string Username { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
        public DateTimeOffset ConnectedAt { get; set; }
        public int RequestCount { get; set; }
        public string? ActiveTransferId { get; set; }
        public string? ActiveProxyId { get; set; }
    }
}
