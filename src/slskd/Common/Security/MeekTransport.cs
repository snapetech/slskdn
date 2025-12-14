// <copyright file="MeekTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using MeekOptions = slskd.Common.Security.MeekTransportOptions;

namespace slskd.Common.Security;

/// <summary>
/// Meek transport for domain fronting.
/// Uses domain fronting through CDNs to bypass censorship.
/// </summary>
public class MeekTransport : IAnonymityTransport
{
    private readonly MeekOptions _options;
    private readonly ILogger<MeekTransport> _logger;
    private readonly HttpClient _httpClient;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MeekTransport"/> class.
    /// </summary>
    /// <param name="options">The meek options.</param>
    /// <param name="logger">The logger.</param>
    public MeekTransport(MeekOptions options, ILogger<MeekTransport> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60) // Meek can be slow due to domain fronting
        };

        // Configure for domain fronting
        _httpClient.DefaultRequestHeaders.Add("Host", _options.FrontDomain);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.Meek;

    /// <summary>
    /// Checks if meek transport is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if meek is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test domain fronting capability
            var request = new HttpRequestMessage(HttpMethod.Get, _options.BridgeUrl);

            // Add domain fronting headers
            request.Headers.Host = _options.FrontDomain;
            request.Headers.Add("X-Meek-Test", "1");

            if (_options.CustomHeaders != null)
            {
                foreach (var header in _options.CustomHeaders)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            lock (_statusLock)
            {
                _status.IsAvailable = response.IsSuccessStatusCode;
                _status.LastError = _status.IsAvailable ? null : $"HTTP {response.StatusCode}";
                if (_status.IsAvailable)
                {
                    _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
                }
            }

            _logger.LogDebug("Meek transport is available via {Url} fronting as {Domain}",
                _options.BridgeUrl, _options.FrontDomain);
            return _status.IsAvailable;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = ex.Message;
            }
            _logger.LogWarning(ex, "Meek transport not available");
            return false;
        }
    }

    /// <summary>
    /// Establishes a connection through meek domain fronting.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the fronted connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through meek domain fronting with stream isolation.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the fronted connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            // Create meek tunnel request
            var sessionId = isolationKey ?? Guid.NewGuid().ToString("N").Substring(0, 8);

            var tunnelRequest = new MeekTunnelRequest
            {
                Host = host,
                Port = port,
                SessionId = sessionId,
                FrontDomain = _options.FrontDomain
            };

            var requestJson = System.Text.Json.JsonSerializer.Serialize(tunnelRequest);
            var encryptedPayload = await EncryptPayloadAsync(requestJson);

            // Send initial connection request
            var request = new HttpRequestMessage(HttpMethod.Post, _options.BridgeUrl)
            {
                Content = new StringContent(encryptedPayload, Encoding.UTF8, "application/octet-stream")
            };

            // Domain fronting: Host header says front domain, but we're actually connecting to bridge
            request.Headers.Host = _options.FrontDomain;
            request.Headers.Add("X-Meek-Session", sessionId);

            if (_options.CustomHeaders != null)
            {
                foreach (var header in _options.CustomHeaders)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Meek tunnel request failed: {response.StatusCode}");
            }

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            _logger.LogDebug("Established meek connection to {Host}:{Port} fronting through {Domain}",
                host, port, _options.FrontDomain);
            return new MeekStream(_httpClient, _options, tunnelRequest, response, () =>
            {
                lock (_statusLock)
                {
                    _status.ActiveConnections = Math.Max(0, _status.ActiveConnections - 1);
                }
            });
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.LastError = ex.Message;
            }
            _logger.LogError(ex, "Failed to establish meek connection to {Host}:{Port}", host, port);
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of the meek transport.
    /// </summary>
    public AnonymityTransportStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new AnonymityTransportStatus
            {
                IsAvailable = _status.IsAvailable,
                LastError = _status.LastError,
                LastSuccessfulConnection = _status.LastSuccessfulConnection,
                ActiveConnections = _status.ActiveConnections,
                TotalConnectionsAttempted = _status.TotalConnectionsAttempted,
                TotalConnectionsSuccessful = _status.TotalConnectionsSuccessful,
            };
        }
    }

    private async Task<string> EncryptPayloadAsync(string payload)
    {
        // In a real implementation, this would encrypt the payload
        // For now, we'll just base64 encode it
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes);
    }

    private async Task<string> DecryptPayloadAsync(string encryptedPayload)
    {
        // In a real implementation, this would decrypt the payload
        // For now, we'll just base64 decode it
        var bytes = Convert.FromBase64String(encryptedPayload);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Meek tunnel request message.
    /// </summary>
    private record MeekTunnelRequest
    {
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public string SessionId { get; init; } = "";
        public string FrontDomain { get; init; } = "";
    }

    /// <summary>
    /// Stream wrapper for meek connections.
    /// </summary>
    private class MeekStream : Stream
    {
        private readonly HttpClient _httpClient;
        private readonly MeekOptions _options;
        private readonly MeekTunnelRequest _tunnelRequest;
        private readonly HttpResponseMessage _initialResponse;
        private readonly Action _onDispose;
        private bool _disposed;
        private Stream? _responseStream;

        public MeekStream(HttpClient httpClient, MeekOptions options, MeekTunnelRequest tunnelRequest, HttpResponseMessage initialResponse, Action onDispose)
        {
            _httpClient = httpClient;
            _options = options;
            _tunnelRequest = tunnelRequest;
            _initialResponse = initialResponse;
            _onDispose = onDispose;
            _responseStream = initialResponse.Content.ReadAsStream();
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_responseStream == null)
            {
                return 0; // EOF
            }

            return await _responseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Send data through additional HTTP requests
            var encryptedData = Convert.ToBase64String(buffer.AsSpan(offset, count).ToArray());

            var request = new HttpRequestMessage(HttpMethod.Post, _options.BridgeUrl)
            {
                Content = new StringContent(encryptedData, Encoding.UTF8, "application/octet-stream")
            };

            request.Headers.Host = _options.FrontDomain;
            request.Headers.Add("X-Meek-Session", _tunnelRequest.SessionId);
            request.Headers.Add("X-Meek-Data", "1");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new IOException($"Meek data transmission failed: {response.StatusCode}");
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _responseStream?.Dispose();
                _initialResponse.Dispose();
                _onDispose();
            }

            base.Dispose(disposing);
        }
    }
}