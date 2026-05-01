// <copyright file="SoulseekObfuscationSupport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd;

/// <summary>
///     Builds the first-class runtime plan for Soulseek type-1 peer-message obfuscation.
/// </summary>
public static class SoulseekObfuscationSupport
{
    /// <summary>
    ///     Soulseek public-server obfuscation metadata type for rotated peer-message streams.
    /// </summary>
    public const int Type1 = 1;

    /// <summary>
    ///     Indicates whether the current Soulseek.NET runtime exposes the type-1 listener and dialer hooks.
    /// </summary>
    public static bool RuntimeSupportsType1PeerMessages => true;

    /// <summary>
    ///     Build a serializable runtime plan for configuration, diagnostics, and the web UI.
    /// </summary>
    /// <param name="soulseek">Soulseek options.</param>
    /// <returns>A runtime plan describing the requested posture and current activation state.</returns>
    public static SoulseekObfuscationPlan BuildPlan(Options.SoulseekOptions soulseek)
    {
        var options = soulseek.Obfuscation;
        var mode = Enum.TryParse<SoulseekObfuscationMode>(options.Mode, ignoreCase: true, out var parsedMode)
            ? parsedMode
            : SoulseekObfuscationMode.Compatibility;

        var requestedListenPort = options.ListenPort > 0 ? options.ListenPort : (int?)null;
        var effectiveListenPort = requestedListenPort ?? DeriveListenPort(soulseek.ListenPort);
        var runtimeState = options.Enabled ? "active" : "disabled";

        var limitations = new List<string>();

        limitations.Add("Type-1 obfuscation is a compatibility/privacy posture, not transport security or meaningful encryption.");
        limitations.Add("Current research covers peer-message streams; file transfer and distributed-network paths remain regular-port based.");

        if (mode == SoulseekObfuscationMode.Only)
        {
            limitations.Add("Only mode is not supported by the current runtime because slskdN keeps the regular peer-message path advertised for legacy compatibility.");
        }

        return new SoulseekObfuscationPlan(
            Enabled: options.Enabled,
            Mode: mode.ToString().ToLowerInvariant(),
            Type: Type1,
            RegularListenPort: soulseek.ListenPort,
            RequestedListenPort: requestedListenPort,
            EffectiveListenPort: effectiveListenPort,
            AdvertiseRegularPort: options.AdvertiseRegularPort,
            PreferOutbound: options.PreferOutbound,
            RuntimeSupported: RuntimeSupportsType1PeerMessages,
            RuntimeState: runtimeState,
            Summary: BuildSummary(options.Enabled, mode),
            Limitations: limitations);
    }

    private static int? DeriveListenPort(int regularListenPort)
        => regularListenPort < 65535 ? regularListenPort + 1 : null;

    /// <summary>
    ///     Build runtime options for the Soulseek client.
    /// </summary>
    /// <param name="soulseek">Soulseek options.</param>
    /// <returns>Runtime peer obfuscation options.</returns>
    public static Soulseek.PeerObfuscationOptions BuildRuntimeOptions(Options.SoulseekOptions soulseek)
    {
        var plan = BuildPlan(soulseek);

        return new Soulseek.PeerObfuscationOptions(
            enabled: plan.Enabled && plan.RuntimeSupported && plan.EffectiveListenPort.HasValue,
            listenPort: plan.EffectiveListenPort ?? 0,
            type: plan.Type,
            advertiseRegularPort: plan.AdvertiseRegularPort,
            preferOutbound: plan.PreferOutbound);
    }

    private static string BuildSummary(bool enabled, SoulseekObfuscationMode mode)
    {
        if (!enabled)
        {
            return "Soulseek type-1 peer-message obfuscation is disabled.";
        }

        var posture = mode switch
        {
            SoulseekObfuscationMode.Compatibility => "Compatibility mode keeps the regular peer-message path available and adds obfuscated reachability.",
            SoulseekObfuscationMode.Prefer => "Prefer mode uses obfuscated peer-message dials when peers advertise type-1 metadata and keeps regular fallback.",
            SoulseekObfuscationMode.Only => "Only mode is not currently supported; the runtime keeps regular peer-message fallback for legacy compatibility.",
            _ => "Soulseek type-1 peer-message obfuscation is configured.",
        };

        return posture;
    }
}

/// <summary>
///     Serializable Soulseek type-1 obfuscation runtime plan.
/// </summary>
/// <param name="Enabled">Whether the feature option is enabled.</param>
/// <param name="Mode">Configured posture.</param>
/// <param name="Type">Soulseek obfuscation type.</param>
/// <param name="RegularListenPort">Regular peer-message listen port.</param>
/// <param name="RequestedListenPort">Configured obfuscated listen port, if explicitly set.</param>
/// <param name="EffectiveListenPort">Effective obfuscated listen port when runtime support exists.</param>
/// <param name="AdvertiseRegularPort">Whether regular-port metadata is advertised alongside obfuscation metadata.</param>
/// <param name="PreferOutbound">Whether outbound peer-message dials prefer compatible obfuscated metadata.</param>
/// <param name="RuntimeSupported">Whether the current runtime can activate type-1 peer-message obfuscation.</param>
/// <param name="RuntimeState">Current activation state.</param>
/// <param name="Summary">Human-readable status.</param>
/// <param name="Limitations">Known limitations and compatibility warnings.</param>
public sealed record SoulseekObfuscationPlan(
    bool Enabled,
    string Mode,
    int Type,
    int RegularListenPort,
    int? RequestedListenPort,
    int? EffectiveListenPort,
    bool AdvertiseRegularPort,
    bool PreferOutbound,
    bool RuntimeSupported,
    string RuntimeState,
    string Summary,
    IReadOnlyList<string> Limitations);
