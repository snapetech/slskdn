// <copyright file="MeshTransportOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Tor stream isolation methods.
/// </summary>
public enum TorIsolationMethod
{
    /// <summary>
    /// Use SOCKS5 username/password authentication for per-peer circuit isolation.
    /// </summary>
    SocksAuth,

    /// <summary>
    /// Use Tor control port to create isolated circuits (not implemented in MVP).
    /// </summary>
    ControlPort
}

/// <summary>
/// Configuration options for mesh transport connectivity.
/// Controls which transports are enabled and how they are configured.
/// </summary>
public class MeshTransportOptions
{
    /// <summary>
    /// Gets or sets whether direct (clearnet) connectivity is enabled.
    /// </summary>
    public bool EnableDirect { get; set; } = true;

    /// <summary>
    /// Gets or sets Tor transport options.
    /// </summary>
    public TorTransportOptions Tor { get; set; } = new();

    /// <summary>
    /// Gets or sets I2P transport options.
    /// </summary>
    public I2PTransportOptions I2P { get; set; } = new();

    /// <summary>
    /// Gets or sets the default transport preference order.
    /// Lower preference values are tried first.
    /// </summary>
    public List<TransportType> PreferenceOrder { get; set; } = new()
    {
        TransportType.DirectQuic,
        TransportType.TorOnionQuic,
        TransportType.I2PQuic
    };
}

/// <summary>
/// Tor transport configuration options.
/// </summary>
public class TorTransportOptions
{
    /// <summary>
    /// Gets or sets whether Tor connectivity is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Tor SOCKS proxy host.
    /// </summary>
    public string SocksHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the Tor SOCKS proxy port.
    /// </summary>
    public int SocksPort { get; set; } = 9050;

    /// <summary>
    /// Gets or sets whether to advertise onion service endpoints.
    /// </summary>
    public bool AdvertiseOnion { get; set; } = false;

    /// <summary>
    /// Gets or sets the local port for onion service mapping.
    /// </summary>
    public int? OnionPort { get; set; }

    /// <summary>
    /// Gets or sets the onion service address (if known).
    /// </summary>
    public string? OnionAddress { get; set; }

    /// <summary>
    /// Gets or sets whether to enable privacy mode that omits clearnet endpoints.
    /// </summary>
    public bool PrivacyModeNoClearnetAdvertise { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to allow data plane transfers over Tor.
    /// </summary>
    public bool AllowDataOverTor { get; set; } = false;

    /// <summary>
    /// Gets or sets the connection timeout for Tor connections.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of concurrent Tor connections.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to enable stream isolation for Tor connections.
    /// When enabled, each peer uses separate SOCKS authentication credentials
    /// to ensure different Tor circuits and prevent correlation attacks.
    /// </summary>
    public bool EnableStreamIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets the stream isolation method to use.
    /// </summary>
    public TorIsolationMethod IsolationMethod { get; set; } = TorIsolationMethod.SocksAuth;
}

/// <summary>
/// I2P transport configuration options.
/// </summary>
public class I2PTransportOptions
{
    /// <summary>
    /// Gets or sets whether I2P connectivity is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the I2P SOCKS proxy host.
    /// </summary>
    public string SocksHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the I2P SOCKS proxy port.
    /// </summary>
    public int SocksPort { get; set; } = 4447;

    /// <summary>
    /// Gets or sets whether to advertise I2P destination endpoints.
    /// </summary>
    public bool AdvertiseI2P { get; set; } = false;

    /// <summary>
    /// Gets or sets the I2P destination address (if known).
    /// </summary>
    public string? DestinationAddress { get; set; }

    /// <summary>
    /// Gets or sets whether to allow data plane transfers over I2P.
    /// </summary>
    public bool AllowDataOverI2p { get; set; } = false;

    /// <summary>
    /// Gets or sets the connection timeout for I2P connections.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Gets or sets the maximum number of concurrent I2P connections.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 5;
}
