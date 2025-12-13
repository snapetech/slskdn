namespace slskd.Signals;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Channel handler for delivering signals over BitTorrent extension protocol.
/// </summary>
public class BtExtensionSignalChannelHandler : ISignalChannelHandler
{
    private readonly ILogger<BtExtensionSignalChannelHandler> logger;
    private readonly SignalSystemOptions options;
    private readonly IBtExtensionSender btExtensionSender;
    private readonly string localPeerId;
    private Func<Signal, CancellationToken, Task>? onSignalReceived;

    public BtExtensionSignalChannelHandler(
        ILogger<BtExtensionSignalChannelHandler> logger,
        IOptionsMonitor<SignalSystemOptions> optionsMonitor,
        IBtExtensionSender btExtensionSender,
        string localPeerId)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
        this.btExtensionSender = btExtensionSender ?? throw new InvalidOperationException("IBtExtensionSender not available. BT extension signal channel requires BitTorrent backend to be initialized.");
        this.localPeerId = localPeerId ?? throw new ArgumentNullException(nameof(localPeerId));
    }

    /// <inheritdoc />
    public bool CanSendTo(string peerId)
    {
        if (!options.BtExtensionChannel.Enabled)
            return false;

        if (options.BtExtensionChannel.RequireActiveSession)
        {
            // Check if we have an active BT session with this peer
            return btExtensionSender.HasActiveSession(peerId);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task SendAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        if (signal == null)
            throw new ArgumentNullException(nameof(signal));

        if (!options.BtExtensionChannel.Enabled)
        {
            logger.LogWarning("BT extension channel is disabled, cannot send signal {SignalId}", signal.SignalId);
            return;
        }

        try
        {
            // Serialize signal to CBOR (or JSON for now)
            var signalJson = JsonSerializer.Serialize(signal, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Wrap in slskdn extension message
            var extensionMessage = new SlskdnExtensionMessage
            {
                Kind = SlskdnSignalKind.SignalEnvelope,
                Payload = signalJson
            };

            // Send via BT extension
            await btExtensionSender.SendExtensionMessageAsync(signal.ToPeerId, extensionMessage, cancellationToken);

            logger.LogDebug("Signal {SignalId} sent via BT extension to peer {PeerId}", signal.SignalId, signal.ToPeerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send signal {SignalId} via BT extension", signal.SignalId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StartReceivingAsync(Func<Signal, CancellationToken, Task> onSignalReceived, CancellationToken cancellationToken = default)
    {
        this.onSignalReceived = onSignalReceived ?? throw new ArgumentNullException(nameof(onSignalReceived));

        // Subscribe to slskdn extension messages
        btExtensionSender.OnSlskdnExtensionMessageReceived += HandleIncomingMessage;

        logger.LogInformation("BT extension signal channel handler started receiving");
        return Task.CompletedTask;
    }

    private async Task HandleIncomingMessage(SlskdnExtensionMessage message, string fromPeerId, CancellationToken cancellationToken)
    {
        if (message == null || message.Kind != SlskdnSignalKind.SignalEnvelope || onSignalReceived == null)
            return;

        try
        {
            // Deserialize signal from payload
            var signal = JsonSerializer.Deserialize<Signal>(message.Payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (signal == null)
            {
                logger.LogWarning("Failed to deserialize signal from BT extension message");
                return;
            }

            // Only process if this signal is for us
            if (signal.ToPeerId != localPeerId)
            {
                logger.LogDebug("Ignoring signal {SignalId} not addressed to us (target: {ToPeerId}, local: {LocalPeerId})",
                    signal.SignalId, signal.ToPeerId, localPeerId);
                return;
            }

            await onSignalReceived(signal, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling incoming BT extension signal from peer {PeerId}", fromPeerId);
        }
    }
}

/// <summary>
/// Interface for sending messages via BitTorrent extension protocol.
/// </summary>
public interface IBtExtensionSender
{
    /// <summary>
    /// Check if there's an active BT session with the specified peer.
    /// </summary>
    bool HasActiveSession(string peerId);

    /// <summary>
    /// Send an extension message to a peer via BT extension protocol.
    /// </summary>
    Task SendExtensionMessageAsync(string peerId, SlskdnExtensionMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when a slskdn extension message is received.
    /// </summary>
    event Func<SlskdnExtensionMessage, string, CancellationToken, Task>? OnSlskdnExtensionMessageReceived;
}

/// <summary>
/// BitTorrent extension message for slskdn signals.
/// </summary>
public class SlskdnExtensionMessage
{
    public SlskdnSignalKind Kind { get; set; }
    public string Payload { get; set; } = string.Empty; // CBOR or JSON serialized signal
}

/// <summary>
/// Types of slskdn extension messages.
/// </summary>
public enum SlskdnSignalKind
{
    /// <summary>
    /// Signal envelope containing a Signal object.
    /// </summary>
    SignalEnvelope = 1
}
















