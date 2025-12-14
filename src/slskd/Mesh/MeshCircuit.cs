// <copyright file="MeshCircuit.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using slskd.Common.Security;

namespace slskd.Mesh;

/// <summary>
/// Represents a multi-hop onion routing circuit through mesh peers.
/// </summary>
public class MeshCircuit : IDisposable
{
    private readonly TimeSpan _timeToLive;
    private readonly DateTimeOffset _createdAt;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshCircuit"/> class.
    /// </summary>
    /// <param name="circuitId">The unique circuit ID.</param>
    /// <param name="hops">The circuit hops.</param>
    /// <param name="timeToLive">The circuit time-to-live.</param>
    public MeshCircuit(string circuitId, List<CircuitHop> hops, TimeSpan timeToLive)
    {
        CircuitId = circuitId ?? throw new ArgumentNullException(nameof(circuitId));
        Hops = hops ?? throw new ArgumentNullException(nameof(hops));
        _timeToLive = timeToLive;
        _createdAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique circuit ID.
    /// </summary>
    public string CircuitId { get; }

    /// <summary>
    /// Gets the circuit hops in order.
    /// </summary>
    public List<CircuitHop> Hops { get; }

    /// <summary>
    /// Gets the entry hop (first hop).
    /// </summary>
    public CircuitHop? EntryHop => Hops.FirstOrDefault(h => h.Role == CircuitHopRole.Entry);

    /// <summary>
    /// Gets the exit hop (final hop).
    /// </summary>
    public CircuitHop? ExitHop => Hops.FirstOrDefault(h => h.Role == CircuitHopRole.Exit);

    /// <summary>
    /// Gets the target peer ID (destination of the circuit).
    /// </summary>
    public string? TargetPeerId => ExitHop?.PeerId;

    /// <summary>
    /// Gets a value indicating whether the circuit is complete (all hops established).
    /// </summary>
    public bool IsComplete() => Hops.All(h => h.IsEstablished);

    /// <summary>
    /// Gets a value indicating whether the circuit is expired.
    /// </summary>
    public bool IsExpired() => DateTimeOffset.UtcNow - _createdAt > _timeToLive;

    /// <summary>
    /// Gets the circuit age.
    /// </summary>
    public TimeSpan Age => DateTimeOffset.UtcNow - _createdAt;

    /// <summary>
    /// Gets the remaining time-to-live.
    /// </summary>
    public TimeSpan RemainingTimeToLive => _timeToLive - Age;

    /// <summary>
    /// Sends data through the circuit.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsComplete())
        {
            throw new InvalidOperationException("Cannot send data through incomplete circuit");
        }

        if (IsExpired())
        {
            throw new InvalidOperationException("Cannot send data through expired circuit");
        }

        // In a real onion routing implementation, data would be:
        // 1. Encrypted with the exit node's key
        // 2. Encrypted with the intermediate node keys
        // 3. Encrypted with the entry node key
        // 4. Sent through the entry node

        // For this placeholder, we'll just send through the first established hop
        var entryHop = EntryHop;
        if (entryHop?.Stream == null)
        {
            throw new InvalidOperationException("Entry hop not available");
        }

        await entryHop.Stream.WriteAsync(data, 0, data.Length, cancellationToken);
    }

    /// <summary>
    /// Receives data from the circuit.
    /// </summary>
    /// <param name="buffer">The buffer to receive into.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of bytes received.</returns>
    public async Task<int> ReceiveAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        if (!IsComplete())
        {
            throw new InvalidOperationException("Cannot receive data from incomplete circuit");
        }

        if (IsExpired())
        {
            throw new InvalidOperationException("Cannot receive data from expired circuit");
        }

        // In a real implementation, data would be received from the entry node
        // and decrypted layer by layer as it passes back through the circuit

        var entryHop = EntryHop;
        if (entryHop?.Stream == null)
        {
            throw new InvalidOperationException("Entry hop not available");
        }

        return await entryHop.Stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
    }

    /// <summary>
    /// Gets circuit information for debugging/logging.
    /// </summary>
    public CircuitInfo GetInfo()
    {
        return new CircuitInfo
        {
            CircuitId = CircuitId,
            HopCount = Hops.Count,
            IsComplete = IsComplete(),
            IsExpired = IsExpired(),
            Age = Age,
            RemainingTimeToLive = RemainingTimeToLive,
            EntryPeerId = EntryHop?.PeerId,
            ExitPeerId = ExitHop?.PeerId,
            HopDetails = Hops.Select(h => new HopInfo
            {
                HopNumber = h.HopNumber,
                PeerId = h.PeerId,
                Role = h.Role.ToString(),
                IsEstablished = h.IsEstablished,
                Address = h.PeerAddress?.ToString(),
                TransportType = h.Transport?.TransportType.ToString()
            }).ToList()
        };
    }

    /// <summary>
    /// Disposes the circuit and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var hop in Hops)
                {
                    hop.Dispose();
                }
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a single hop in a mesh circuit.
/// </summary>
public class CircuitHop : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets or sets the hop number (1-based).
    /// </summary>
    public int HopNumber { get; set; }

    /// <summary>
    /// Gets or sets the peer ID for this hop.
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the peer address.
    /// </summary>
    public IPEndPoint? PeerAddress { get; set; }

    /// <summary>
    /// Gets or sets the hop role.
    /// </summary>
    public CircuitHopRole Role { get; set; }

    /// <summary>
    /// Gets or sets the transport used for this hop.
    /// </summary>
    public IAnonymityTransport? Transport { get; set; }

    /// <summary>
    /// Gets or sets the stream for this hop.
    /// </summary>
    public Stream? Stream { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this hop is established.
    /// </summary>
    public bool IsEstablished { get; set; }

    /// <summary>
    /// Gets or sets the error message if establishment failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when this hop was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Disposes resources for this hop.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Stream?.Dispose();
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Information about a circuit for debugging/logging.
/// </summary>
public class CircuitInfo
{
    /// <summary>
    /// Gets or sets the circuit ID.
    /// </summary>
    public string CircuitId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of hops.
    /// </summary>
    public int HopCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the circuit is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the circuit is expired.
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// Gets or sets the circuit age.
    /// </summary>
    public TimeSpan Age { get; set; }

    /// <summary>
    /// Gets or sets the remaining time-to-live.
    /// </summary>
    public TimeSpan RemainingTimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the entry peer ID.
    /// </summary>
    public string? EntryPeerId { get; set; }

    /// <summary>
    /// Gets or sets the exit peer ID.
    /// </summary>
    public string? ExitPeerId { get; set; }

    /// <summary>
    /// Gets or sets the hop details.
    /// </summary>
    public List<HopInfo> HopDetails { get; set; } = new();
}

/// <summary>
/// Information about a circuit hop.
/// </summary>
public class HopInfo
{
    /// <summary>
    /// Gets or sets the hop number.
    /// </summary>
    public int HopNumber { get; set; }

    /// <summary>
    /// Gets or sets the peer ID.
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hop role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the hop is established.
    /// </summary>
    public bool IsEstablished { get; set; }

    /// <summary>
    /// Gets or sets the peer address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the transport type.
    /// </summary>
    public string? TransportType { get; set; }
}

