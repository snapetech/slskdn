// <copyright file="TransportEndpoint.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using MessagePack;

namespace slskd.Mesh;

/// <summary>
/// Type of transport endpoint.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Direct QUIC connection over clearnet.
    /// </summary>
    DirectQuic,

    /// <summary>
    /// Tor onion service QUIC connection.
    /// </summary>
    TorOnionQuic,

    /// <summary>
    /// I2P destination QUIC connection.
    /// </summary>
    I2PQuic
}

/// <summary>
/// Scope flags for transport endpoints.
/// </summary>
[Flags]
public enum TransportScope
{
    /// <summary>
    /// Endpoint can be used for control plane messages.
    /// </summary>
    Control = 1,

    /// <summary>
    /// Endpoint can be used for data plane transfers.
    /// </summary>
    Data = 2,

    /// <summary>
    /// Endpoint can be used for both control and data.
    /// </summary>
    ControlAndData = Control | Data
}

/// <summary>
/// Transport endpoint record for mesh peer descriptors.
/// Defines how to reach a peer via different transport methods.
/// </summary>
[MessagePackObject]
public class TransportEndpoint
{
    /// <summary>
    /// Gets or sets the type of transport.
    /// </summary>
    [Key(0)]
    public TransportType TransportType { get; set; }

    /// <summary>
    /// Gets or sets the host (IP, hostname, onion address, or I2P destination).
    /// </summary>
    [Key(1)]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number.
    /// </summary>
    [Key(2)]
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the scope flags indicating what this endpoint can be used for.
    /// </summary>
    [Key(3)]
    public TransportScope Scope { get; set; } = TransportScope.ControlAndData;

    /// <summary>
    /// Gets or sets the preference value (lower = more preferred).
    /// </summary>
    [Key(4)]
    public int Preference { get; set; } = 0;

    /// <summary>
    /// Gets or sets the cost value (higher = more expensive to use).
    /// </summary>
    [Key(5)]
    public int Cost { get; set; } = 0;

    /// <summary>
    /// Gets or sets the Unix timestamp when this endpoint becomes valid (optional).
    /// </summary>
    [Key(6)]
    public long? ValidFromUnixMs { get; set; }

    /// <summary>
    /// Gets or sets the Unix timestamp when this endpoint expires (optional).
    /// </summary>
    [Key(7)]
    public long? ValidToUnixMs { get; set; }

    /// <summary>
    /// Determines if this endpoint is currently valid based on timestamps.
    /// </summary>
    public bool IsValid()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (ValidFromUnixMs.HasValue && now < ValidFromUnixMs.Value)
        {
            return false; // Not yet valid
        }

        if (ValidToUnixMs.HasValue && now > ValidToUnixMs.Value)
        {
            return false; // Expired
        }

        return true;
    }

    /// <summary>
    /// Gets a human-readable description of the endpoint.
    /// </summary>
    public override string ToString()
    {
        var scopeStr = Scope switch
        {
            TransportScope.Control => "control",
            TransportScope.Data => "data",
            TransportScope.ControlAndData => "control+data",
            _ => "unknown"
        };

        return $"{TransportType}://{Host}:{Port} ({scopeStr}, pref:{Preference}, cost:{Cost})";
    }
}


