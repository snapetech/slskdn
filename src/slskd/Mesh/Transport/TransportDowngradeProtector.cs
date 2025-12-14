// <copyright file="TransportDowngradeProtector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Transport;

/// <summary>
/// Protects against transport downgrade attacks by enforcing minimum security requirements.
/// </summary>
public class TransportDowngradeProtector
{
    private readonly ILogger<TransportDowngradeProtector> _logger;

    public TransportDowngradeProtector(ILogger<TransportDowngradeProtector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Determines the minimum acceptable transport security level for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="hasEstablishedTrust">Whether trust has been previously established.</param>
    /// <param name="connectionHistory">Historical connection data.</param>
    /// <returns>The minimum transport security level.</returns>
    public TransportSecurityLevel GetMinimumSecurityLevel(
        string peerId,
        bool hasEstablishedTrust,
        ConnectionHistory? connectionHistory = null)
    {
        // For new peers, require at least basic security
        if (!hasEstablishedTrust)
        {
            return TransportSecurityLevel.Basic;
        }

        // If we've successfully connected via private transports before,
        // maintain that security level
        if (connectionHistory?.HasSuccessfulPrivateConnections == true)
        {
            return TransportSecurityLevel.Private;
        }

        // Default to allowing any configured transport
        return TransportSecurityLevel.Any;
    }

    /// <summary>
    /// Validates that a transport selection meets security requirements.
    /// </summary>
    /// <param name="selectedTransport">The selected transport type.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="minimumLevel">The minimum required security level.</param>
    /// <param name="availableEndpoints">Available endpoints for the peer.</param>
    /// <returns>Validation result.</returns>
    public DowngradeValidationResult ValidateTransportSelection(
        TransportType selectedTransport,
        string peerId,
        TransportSecurityLevel minimumLevel,
        IEnumerable<TransportEndpoint> availableEndpoints)
    {
        var transportLevel = GetTransportSecurityLevel(selectedTransport);

        // Check if selected transport meets minimum requirements
        if (transportLevel < minimumLevel)
        {
            // Check if better alternatives were available
            var betterAlternatives = availableEndpoints
                .Where(ep => GetTransportSecurityLevel(ep.TransportType) >= minimumLevel)
                .ToList();

            if (betterAlternatives.Any())
            {
                _logger.LogWarning("Transport downgrade detected for peer {PeerId}: selected {Selected} (level {SelectedLevel}) but {BetterCount} better alternatives available requiring level {MinLevel}",
                    peerId, selectedTransport, transportLevel, betterAlternatives.Count, minimumLevel);

                return DowngradeValidationResult.DowngradeDetected(
                    $"Transport downgrade: {selectedTransport} below required security level {minimumLevel}. " +
                    $"{betterAlternatives.Count} more secure alternatives available.");
            }
            else
            {
                // No better alternatives available, allow the selection but log
                _logger.LogInformation("Transport selection for peer {PeerId} is below preferred level {MinLevel} but no alternatives available",
                    peerId, minimumLevel);
            }
        }

        return DowngradeValidationResult.Valid();
    }

    /// <summary>
    /// Detects potential downgrade attacks based on connection pattern analysis.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="recentConnections">Recent connection attempts.</param>
    /// <returns>Downgrade detection result.</returns>
    public DowngradeDetectionResult AnalyzeConnectionPatterns(
        string peerId,
        IEnumerable<ConnectionAttempt> recentConnections)
    {
        var connections = recentConnections.ToList();
        if (connections.Count < 3)
        {
            return DowngradeDetectionResult.NoPatternDetected();
        }

        // Look for pattern of failing private connections then succeeding with clearnet
        var privateFailures = connections
            .Where(c => !c.WasSuccessful &&
                       (c.TransportType == TransportType.TorOnionQuic || c.TransportType == TransportType.I2PQuic))
            .ToList();

        var clearnetSuccesses = connections
            .Where(c => c.WasSuccessful && c.TransportType == TransportType.DirectQuic)
            .ToList();

        if (privateFailures.Any() && clearnetSuccesses.Any())
        {
            var latestPrivateFailure = privateFailures.Max(c => c.Timestamp);
            var earliestClearnetSuccess = clearnetSuccesses.Min(c => c.Timestamp);

            if (earliestClearnetSuccess > latestPrivateFailure)
            {
                _logger.LogWarning("Potential downgrade attack pattern detected for peer {PeerId}: private connections failed, then clearnet succeeded",
                    peerId);

                return DowngradeDetectionResult.CreatePatternDetected(
                    "Suspicious pattern: private transport failures followed by clearnet success");
            }
        }

        return DowngradeDetectionResult.NoPatternDetected();
    }

    private TransportSecurityLevel GetTransportSecurityLevel(TransportType transportType)
    {
        return transportType switch
        {
            TransportType.DirectQuic => TransportSecurityLevel.Clearnet,
            TransportType.TorOnionQuic => TransportSecurityLevel.Private,
            TransportType.I2PQuic => TransportSecurityLevel.Private,
            _ => TransportSecurityLevel.Any
        };
    }
}

/// <summary>
/// Transport security levels for downgrade protection.
/// </summary>
public enum TransportSecurityLevel
{
    /// <summary>
    /// Any configured transport is acceptable.
    /// </summary>
    Any = 0,

    /// <summary>
    /// At least basic security (TLS) required.
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Private transports (Tor/I2P) required.
    /// </summary>
    Private = 2,

    /// <summary>
    /// Clearnet transports only (least secure).
    /// </summary>
    Clearnet = 3
}

/// <summary>
/// Result of transport downgrade validation.
/// </summary>
public class DowngradeValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private DowngradeValidationResult() { }

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static DowngradeValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result with an error message.
    /// </summary>
    public static DowngradeValidationResult DowngradeDetected(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

/// <summary>
    /// Result of downgrade pattern detection.
/// </summary>
public class DowngradeDetectionResult
{
    /// <summary>
    /// Gets a value indicating whether a suspicious pattern was detected.
    /// </summary>
    public bool PatternDetected { get; private set; }

    /// <summary>
    /// Gets the description of the detected pattern.
    /// </summary>
    public string? PatternDescription { get; private set; }

    private DowngradeDetectionResult() { }

    /// <summary>
    /// Creates a result indicating no suspicious pattern.
    /// </summary>
    public static DowngradeDetectionResult NoPatternDetected() =>
        new() { PatternDetected = false };

    /// <summary>
    /// Creates a result indicating a suspicious pattern was detected.
    /// </summary>
    public static DowngradeDetectionResult CreatePatternDetected(string description) =>
        new() { PatternDetected = true, PatternDescription = description };
}

/// <summary>
/// Historical connection data for downgrade analysis.
/// </summary>
public class ConnectionHistory
{
    /// <summary>
    /// Gets or sets a value indicating whether successful private connections have been made.
    /// </summary>
    public bool HasSuccessfulPrivateConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of connection attempts.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful connections.
    /// </summary>
    public int SuccessfulConnections { get; set; }
}

/// <summary>
/// Represents a connection attempt for pattern analysis.
/// </summary>
public class ConnectionAttempt
{
    /// <summary>
    /// Gets or sets the transport type used.
    /// </summary>
    public TransportType TransportType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection was successful.
    /// </summary>
    public bool WasSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the attempt.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
