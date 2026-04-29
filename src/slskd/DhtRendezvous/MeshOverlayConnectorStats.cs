// <copyright file="MeshOverlayConnectorStats.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

/// <summary>
/// Statistics for the mesh overlay connector.
/// </summary>
public class MeshOverlayConnectorStats
{
    /// <summary>
    /// Gets or sets the number of pending connection attempts.
    /// </summary>
    public int PendingConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of successful connections.
    /// </summary>
    public long SuccessfulConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed connections.
    /// </summary>
    public long FailedConnections { get; set; }

    /// <summary>
    /// Gets or sets the classified outbound failure counts.
    /// </summary>
    public OverlayConnectionFailureStats FailureReasons { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of connection attempts skipped because an endpoint is cooling down after repeated failures.
    /// </summary>
    public long EndpointCooldownSkips { get; set; }

    /// <summary>
    /// Gets or sets the most degraded endpoints by failure streak.
    /// </summary>
    public IReadOnlyList<OverlayEndpointHealthStats> TopProblemEndpoints { get; set; } = Array.Empty<OverlayEndpointHealthStats>();

    /// <summary>
    /// Gets the connection success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessfulConnections + FailedConnections;
            return total > 0 ? (double)SuccessfulConnections / total : 0.0;
        }
    }
}

/// <summary>
/// Classified outbound failure counts for the mesh overlay connector.
/// </summary>
public class OverlayConnectionFailureStats
{
    public long ConnectTimeouts { get; set; }
    public long NoRouteFailures { get; set; }
    public long ConnectionRefusedFailures { get; set; }
    public long ConnectionResetFailures { get; set; }
    public long TlsEofFailures { get; set; }
    public long TlsHandshakeFailures { get; set; }
    public long ProtocolHandshakeFailures { get; set; }
    public long RegistrationFailures { get; set; }
    public long BlockedPeerFailures { get; set; }
    public long UnknownFailures { get; set; }
}

/// <summary>
/// Health snapshot for a recently attempted overlay endpoint.
/// </summary>
public sealed class OverlayEndpointHealthStats
{
    public string Endpoint { get; set; } = string.Empty;
    public int ConsecutiveFailureCount { get; set; }
    public long TotalFailures { get; set; }
    public string LastFailureReason { get; set; } = string.Empty;
    public DateTimeOffset? LastFailureAt { get; set; }
    public DateTimeOffset? SuppressedUntil { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastUsername { get; set; }
}
