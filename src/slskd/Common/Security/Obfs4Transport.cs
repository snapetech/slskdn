// <copyright file="Obfs4Transport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Obfs4Options = slskd.Common.Security.Obfs4TransportOptions;

namespace slskd.Common.Security;

/// <summary>
/// Obfs4 transport for anti-censorship.
/// Uses Tor's obfs4 protocol to make traffic appear random to DPI systems.
/// </summary>
public class Obfs4Transport : IAnonymityTransport
{
    private readonly Obfs4Options _options;
    private readonly ILogger<Obfs4Transport> _logger;
    private readonly IObfs4VersionChecker _versionChecker;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Obfs4Transport"/> class.
    /// </summary>
    /// <param name="options">The obfs4 options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="versionChecker">Optional. Used for IsAvailableAsync; when null, uses the default process-based check.</param>
    public Obfs4Transport(Obfs4Options options, ILogger<Obfs4Transport> logger, IObfs4VersionChecker? versionChecker = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _versionChecker = versionChecker ?? new Obfs4VersionChecker();
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.Obfs4;

    /// <summary>
    /// Checks if obfs4 transport is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if obfs4 is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if obfs4proxy binary exists
            if (!File.Exists(_options.Obfs4ProxyPath))
            {
                lock (_statusLock)
                {
                    _status.IsAvailable = false;
                    _status.LastError = $"obfs4proxy binary not found at {_options.Obfs4ProxyPath}";
                }
                return false;
            }

            var exitCode = await _versionChecker.RunVersionCheckAsync(_options.Obfs4ProxyPath, cancellationToken).ConfigureAwait(false);
            var isAvailable = exitCode == 0;

            lock (_statusLock)
            {
                _status.IsAvailable = isAvailable;
                _status.LastError = isAvailable ? null : "obfs4proxy version check failed";
                if (isAvailable)
                {
                    _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
                }
            }

            _logger.LogDebug("Obfs4 transport is available (obfs4proxy at {Path})", _options.Obfs4ProxyPath);
            return isAvailable;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = ex.Message;
            }
            _logger.LogWarning(ex, "Obfs4 transport not available");
            return false;
        }
    }

    /// <summary>
    /// Establishes a connection through obfs4.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the obfuscated connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through obfs4 with stream isolation.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the obfuscated connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            // Find a suitable bridge
            var bridge = SelectBridge(host, port);
            if (bridge == null)
            {
                throw new InvalidOperationException("No suitable obfs4 bridge found for the target");
            }

            // Start obfs4proxy process for this connection
            var process = await StartObfs4ProxyAsync(bridge, isolationKey, cancellationToken);

            // Connect to the local obfs4proxy endpoint
            var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", process.LocalPort, cancellationToken);

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            _logger.LogDebug("Established obfs4 connection to {Host}:{Port} via bridge {Bridge}", host, port, bridge.Address);
            return new Obfs4Stream(client.GetStream(), process, () =>
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
            _logger.LogError(ex, "Failed to establish obfs4 connection to {Host}:{Port}", host, port);
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of the obfs4 transport.
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

    private Obfs4Bridge? SelectBridge(string targetHost, int targetPort)
    {
        if (_options.BridgeLines == null || _options.BridgeLines.Count == 0)
        {
            return null;
        }

        // Parse bridge lines and select one that can reach the target
        foreach (var bridgeLine in _options.BridgeLines)
        {
            var bridge = ParseBridgeLine(bridgeLine);
            if (bridge != null && CanBridgeReachTarget(bridge, targetHost, targetPort))
            {
                return bridge;
            }
        }

        // Fallback: return first bridge if none match target criteria
        return ParseBridgeLine(_options.BridgeLines[0]);
    }

    private Obfs4Bridge? ParseBridgeLine(string bridgeLine)
    {
        // Parse obfs4 bridge line format:
        // obfs4 <ip>:<port> <fingerprint> cert=<cert> iat-mode=<mode>
        var match = Regex.Match(bridgeLine, @"obfs4\s+(\S+):(\d+)\s+(\S+)\s+cert=(\S+)\s+iat-mode=(\d+)");
        if (!match.Success)
        {
            _logger.LogWarning("Invalid obfs4 bridge line format: {Line}", bridgeLine);
            return null;
        }

        return new Obfs4Bridge
        {
            Address = match.Groups[1].Value,
            Port = int.Parse(match.Groups[2].Value),
            Fingerprint = match.Groups[3].Value,
            Cert = match.Groups[4].Value,
            IatMode = int.Parse(match.Groups[5].Value)
        };
    }

    private bool CanBridgeReachTarget(Obfs4Bridge bridge, string targetHost, int targetPort)
    {
        // Basic check: if target is in same general geographic region as bridge
        // In practice, this would be more sophisticated
        return true; // For now, assume all bridges can reach all targets
    }

    private async Task<Obfs4Process> StartObfs4ProxyAsync(Obfs4Bridge bridge, string? isolationKey, CancellationToken cancellationToken)
    {
        // Find an available local port
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var localPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        // Start obfs4proxy process
        var arguments = $"client -log-min-severity=warn 127.0.0.1:{localPort}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.Obfs4ProxyPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    ["TOR_PT_CLIENT_TRANSPORTS"] = "obfs4",
                    ["TOR_PT_MANAGED_TRANSPORT_VER"] = "1",
                    ["TOR_PT_STATE_LOCATION"] = Path.GetTempPath(),
                    ["TOR_PT_EXIT_ON_STDIN_CLOSE"] = "0"
                }
            }
        };

        // Set up bridge configuration via environment
        process.StartInfo.Environment["TOR_PT_CLIENT_TRANSPORTS"] = "obfs4";
        process.StartInfo.Environment[$"TOR_PT_CLIENT_TRANSPORT_obfs4_OPT"] =
            $"node-id={bridge.Fingerprint},iat-mode={bridge.IatMode},cert={bridge.Cert}";

        process.Start();

        // Wait for obfs4proxy to be ready (it writes to stdout when ready)
        var ready = await WaitForObfs4ProxyReadyAsync(process, cancellationToken);

        if (!ready)
        {
            process.Kill();
            throw new Exception("obfs4proxy failed to start properly");
        }

        return new Obfs4Process(process, localPort, bridge);
    }

    private async Task<bool> WaitForObfs4ProxyReadyAsync(Process process, CancellationToken cancellationToken)
    {
        // obfs4proxy signals readiness by writing "VERSION 1" to stdout
        var outputTask = process.StandardOutput.ReadLineAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        var completedTask = await Task.WhenAny(outputTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            return false;
        }

        var output = await outputTask;
        return output?.Contains("VERSION 1") == true;
    }

    /// <summary>
    /// Represents an obfs4 bridge.
    /// </summary>
    private class Obfs4Bridge
    {
        public string Address { get; init; } = "";
        public int Port { get; init; }
        public string Fingerprint { get; init; } = "";
        public string Cert { get; init; } = "";
        public int IatMode { get; init; }
    }

    /// <summary>
    /// Represents a running obfs4proxy process.
    /// </summary>
    private class Obfs4Process
    {
        public Process Process { get; }
        public int LocalPort { get; }
        public Obfs4Bridge Bridge { get; }

        public Obfs4Process(Process process, int localPort, Obfs4Bridge bridge)
        {
            Process = process;
            LocalPort = localPort;
            Bridge = bridge;
        }
    }

    /// <summary>
    /// Stream wrapper for obfs4 connections.
    /// </summary>
    private class Obfs4Stream : Stream
    {
        private readonly Stream _innerStream;
        private readonly Obfs4Process _process;
        private readonly Action _onDispose;
        private bool _disposed;

        public Obfs4Stream(Stream innerStream, Obfs4Process process, Action onDispose)
        {
            _innerStream = innerStream;
            _process = process;
            _onDispose = onDispose;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanWrite => _innerStream.CanWrite;
        public override bool CanSeek => _innerStream.CanSeek;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) =>
            _innerStream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) =>
            _innerStream.Write(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

        public override void Flush() => _innerStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _innerStream.FlushAsync(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _innerStream.Dispose();

                try
                {
                    if (!_process.Process.HasExited)
                    {
                        _process.Process.Kill();
                        _process.Process.WaitForExit(1000);
                    }
                    _process.Process.Dispose();
                }
                catch
                {
                    // Ignore process cleanup errors
                }

                _onDispose();
            }

            base.Dispose(disposing);
        }
    }
}