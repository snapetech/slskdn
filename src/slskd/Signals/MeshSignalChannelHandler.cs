namespace slskd.Signals;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Messages;

/// <summary>
/// Channel handler for delivering signals over the Mesh overlay network.
/// </summary>
public class MeshSignalChannelHandler : ISignalChannelHandler
{
    private readonly ILogger<MeshSignalChannelHandler> logger;
    private readonly SignalSystemOptions options;
    private readonly IMeshMessageSender meshSender;
    private readonly string localPeerId;
    private Func<Signal, CancellationToken, Task>? onSignalReceived;

    public MeshSignalChannelHandler(
        ILogger<MeshSignalChannelHandler> logger,
        IOptionsMonitor<SignalSystemOptions> optionsMonitor,
        IMeshMessageSender? meshSender,
        string localPeerId)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
        this.meshSender = meshSender ?? throw new InvalidOperationException("IMeshMessageSender not available. Mesh signal channel requires MeshCore to be initialized.");
        this.localPeerId = localPeerId ?? throw new ArgumentNullException(nameof(localPeerId));
    }

    /// <inheritdoc />
    public bool CanSendTo(string peerId)
    {
        if (!options.MeshChannel.Enabled)
            return false;

        // TODO: Check if Mesh has a route to this peer
        // For now, assume we can send if Mesh is enabled
        return true;
    }

    /// <inheritdoc />
    public async Task SendAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        if (signal == null)
            throw new ArgumentNullException(nameof(signal));

        if (!options.MeshChannel.Enabled)
        {
            logger.LogWarning("Mesh channel is disabled, cannot send signal {SignalId}", signal.SignalId);
            return;
        }

        try
        {
            // Serialize signal to JSON
            var signalJson = JsonSerializer.Serialize(signal, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Wrap in SlskdnSignal message envelope
            var envelope = new SlskdnSignalMessage
            {
                SignalId = signal.SignalId,
                FromPeerId = signal.FromPeerId,
                ToPeerId = signal.ToPeerId,
                Type = signal.Type,
                Body = signalJson,
                SentAt = signal.SentAt,
                Ttl = signal.Ttl
            };

            // Send via Mesh overlay
            await meshSender.SendToPeerAsync(signal.ToPeerId, envelope, cancellationToken);

            logger.LogDebug("Signal {SignalId} sent via Mesh to peer {PeerId}", signal.SignalId, signal.ToPeerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send signal {SignalId} via Mesh", signal.SignalId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StartReceivingAsync(Func<Signal, CancellationToken, Task> onSignalReceived, CancellationToken cancellationToken = default)
    {
        this.onSignalReceived = onSignalReceived ?? throw new ArgumentNullException(nameof(onSignalReceived));

        // Subscribe to SlskdnSignal messages from Mesh
        meshSender.OnSlskdnSignalReceived += HandleIncomingSignal;

        logger.LogInformation("Mesh signal channel handler started receiving");
        return Task.CompletedTask;
    }

    private async Task HandleIncomingSignal(SlskdnSignalMessage envelope, CancellationToken cancellationToken)
    {
        if (envelope == null || onSignalReceived == null)
            return;

        try
        {
            // Deserialize signal body
            var bodyDict = JsonSerializer.Deserialize<Dictionary<string, object>>(envelope.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new Dictionary<string, object>();

            // Reconstruct Signal object
            var signal = new Signal(
                signalId: envelope.SignalId,
                fromPeerId: envelope.FromPeerId,
                toPeerId: envelope.ToPeerId,
                sentAt: envelope.SentAt,
                type: envelope.Type,
                body: bodyDict,
                ttl: envelope.Ttl,
                preferredChannels: new[] { SignalChannel.Mesh } // Incoming signals don't need preferred channels
            );

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
            logger.LogError(ex, "Error handling incoming Mesh signal {SignalId}", envelope.SignalId);
        }
    }
}

/// <summary>
/// Interface for sending messages via Mesh overlay.
/// </summary>
public interface IMeshMessageSender
{
    /// <summary>
    /// Send a message to a specific peer via Mesh overlay.
    /// </summary>
    Task SendToPeerAsync(string peerId, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when a SlskdnSignal message is received.
    /// </summary>
    event Func<SlskdnSignalMessage, CancellationToken, Task>? OnSlskdnSignalReceived;
}

/// <summary>
/// Mesh message envelope for slskdn signals.
/// </summary>
public class SlskdnSignalMessage
{
    public string SignalId { get; set; } = string.Empty;
    public string FromPeerId { get; set; } = string.Empty;
    public string ToPeerId { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty; // JSON-serialized signal body
    public TimeSpan Ttl { get; set; }
}
















