// <copyright file="HttpTunnelTransport.cs" company="slskdN Team">
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
using HttpTunnelOptions = slskd.Common.Security.HttpTunnelTransportOptions;

namespace slskd.Common.Security;

/// <summary>
/// HTTP tunnel transport for DPI circumvention.
/// Encodes traffic as HTTP requests/responses to appear as normal web traffic.
/// </summary>
public class HttpTunnelTransport : IAnonymityTransport
{
    private readonly HttpTunnelOptions _options;
    private readonly ILogger<HttpTunnelTransport> _logger;
    private readonly HttpClient _httpClient;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTunnelTransport"/> class.
    /// </summary>
    /// <param name="options">The HTTP tunnel options.</param>
    /// <param name="logger">The logger.</param>
    public HttpTunnelTransport(HttpTunnelOptions options, ILogger<HttpTunnelTransport> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Configure HTTP client for tunneling
        if (_options.UseHttps)
        {
            // In production, validate certificates properly
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent ?? "Mozilla/5.0 (compatible; slskdN)");
        }
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.HttpTunnel;

    /// <summary>
    /// Checks if HTTP tunnel transport is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if HTTP tunnel is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test connectivity with a simple HEAD request
            var request = new HttpRequestMessage(HttpMethod.Head, _options.ProxyUrl);

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

            _logger.LogDebug("HTTP tunnel transport is available at {Url}", _options.ProxyUrl);
            return _status.IsAvailable;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = ex.Message;
            }
            _logger.LogWarning(ex, "HTTP tunnel transport not available at {Url}", _options.ProxyUrl);
            return false;
        }
    }

    /// <summary>
    /// Establishes a connection through HTTP tunnel.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the tunneled connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through HTTP tunnel with stream isolation.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the tunneled connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            // Create tunnel request
            var tunnelRequest = new TunnelRequest
            {
                Host = host,
                Port = port,
                IsolationKey = isolationKey,
                Method = _options.Method
            };

            var requestJson = System.Text.Json.JsonSerializer.Serialize(tunnelRequest);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(_options.Method switch
            {
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "GET" => HttpMethod.Get,
                _ => HttpMethod.Post
            }, _options.ProxyUrl)
            {
                Content = _options.Method != "GET" ? content : null
            };

            // Add custom headers to appear as normal web traffic
            if (_options.CustomHeaders != null)
            {
                foreach (var header in _options.CustomHeaders)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            // Add isolation header if provided
            if (!string.IsNullOrEmpty(isolationKey))
            {
                request.Headers.Add("X-Tunnel-Isolation", isolationKey);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP tunnel request failed: {response.StatusCode}");
            }

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            _logger.LogDebug("Established HTTP tunnel to {Host}:{Port} via {Url}", host, port, _options.ProxyUrl);
            return new HttpTunnelStream(_httpClient, _options, tunnelRequest, response, () =>
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
            _logger.LogError(ex, "Failed to establish HTTP tunnel to {Host}:{Port}", host, port);
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of the HTTP tunnel transport.
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

    /// <summary>
    /// HTTP tunnel request message.
    /// </summary>
    private record TunnelRequest
    {
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public string? IsolationKey { get; init; }
        public string Method { get; init; } = "POST";
    }

    /// <summary>
    /// Stream wrapper for HTTP tunnel connections.
    /// </summary>
    private class HttpTunnelStream : Stream
    {
        private readonly HttpClient _httpClient;
        private readonly HttpTunnelOptions _options;
        private readonly TunnelRequest _tunnelRequest;
        private readonly HttpResponseMessage _initialResponse;
        private readonly Action _onDispose;
        private bool _disposed;
        private Stream? _responseStream;

        public HttpTunnelStream(HttpClient httpClient, HttpTunnelOptions options, TunnelRequest tunnelRequest, HttpResponseMessage initialResponse, Action onDispose)
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
            // For HTTP tunneling, we need to make subsequent requests to send data
            // This is a simplified implementation - in practice, you'd need a more sophisticated
            // protocol for bidirectional communication over HTTP
            var content = new ByteArrayContent(buffer, offset, count);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var request = new HttpRequestMessage(HttpMethod.Post, _options.ProxyUrl)
            {
                Content = content
            };

            // Add tunnel metadata
            request.Headers.Add("X-Tunnel-Host", _tunnelRequest.Host);
            request.Headers.Add("X-Tunnel-Port", _tunnelRequest.Port.ToString());
            if (_tunnelRequest.IsolationKey != null)
            {
                request.Headers.Add("X-Tunnel-Isolation", _tunnelRequest.IsolationKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new IOException($"HTTP tunnel write failed: {response.StatusCode}");
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