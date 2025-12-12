using System;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Interface for mesh-exposed services.
/// Services implement this to handle incoming RPC calls and streams.
/// </summary>
public interface IMeshService
{
    /// <summary>
    /// Logical service name, must be stable and match the ServiceName in MeshServiceDescriptor.
    /// Examples: "pods", "shadow-index", "mesh-introspect"
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Handles a single request/response style call.
    /// </summary>
    /// <param name="call">The incoming service call.</param>
    /// <param name="context">Context for the call (peer identity, security, logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service reply with status code and payload.</returns>
    Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional: Handles streaming for long-lived bidirectional flows.
    /// If not supported, throw NotSupportedException.
    /// </summary>
    /// <param name="stream">The bidirectional stream.</param>
    /// <param name="context">Context for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context provided to service handlers with peer identity and security info.
/// </summary>
public class MeshServiceContext
{
    /// <summary>
    /// Remote peer ID that initiated this call.
    /// </summary>
    public required string RemotePeerId { get; init; }

    /// <summary>
    /// Public key of the remote peer (if available).
    /// </summary>
    public string? RemotePublicKey { get; init; }

    /// <summary>
    /// When the call was received (UTC).
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional: Correlation ID for tracing.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Optional: Reference to violation tracker for reporting abuse.
    /// </summary>
    public object? ViolationTracker { get; init; }

    /// <summary>
    /// Optional: Logger for service-specific logging.
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger? Logger { get; init; }
}

/// <summary>
/// Represents a bidirectional stream for long-lived service interactions.
/// NOTE: Streaming support is optional and may not be fully implemented in initial version.
/// </summary>
public interface MeshServiceStream
{
    /// <summary>
    /// Sends data to the remote peer.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives data from the remote peer.
    /// </summary>
    /// <returns>Data received, or null if stream ended.</returns>
    Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the stream gracefully.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
