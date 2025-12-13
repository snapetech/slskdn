namespace slskd.Signals;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Signals.Swarm;
using slskd.Security;

/// <summary>
/// Extension methods for registering signal system services.
/// </summary>
public static class SignalServiceExtensions
{
    /// <summary>
    /// Add signal system services to the service collection.
    /// </summary>
    public static IServiceCollection AddSignalSystem(this IServiceCollection services)
    {
        // Register options - bind from main Options.SignalSystem
        services.AddOptions<SignalSystemOptions>()
            .PostConfigure<IOptionsMonitor<slskd.Options>>((signalOptions, optionsMonitor) =>
            {
                var mainOptions = optionsMonitor.CurrentValue;
                if (mainOptions.SignalSystem != null)
                {
                    signalOptions.Enabled = mainOptions.SignalSystem.Enabled;
                    signalOptions.DeduplicationCacheSize = mainOptions.SignalSystem.DeduplicationCacheSize;
                    signalOptions.DefaultTtl = mainOptions.SignalSystem.DefaultTtl;
                    signalOptions.MeshChannel = mainOptions.SignalSystem.MeshChannel;
                    signalOptions.BtExtensionChannel = mainOptions.SignalSystem.BtExtensionChannel;
                }
            })
            .ValidateDataAnnotations();

        // Register SignalBus as singleton
        services.AddSingleton<ISignalBus, SignalBus>();

        // Register channel handlers (will be created when Mesh/BT are available)
        // These are registered as transient since they depend on other services
        // Note: These require IMeshMessageSender/IBtExtensionSender and localPeerId to be provided
        // They should be created manually during initialization, not via DI

        // Register Swarm signal handlers
        // TODO: SwarmSignalHandlers requires string localPeerId parameter - cannot use DI without factory
        // services.AddSingleton<SwarmSignalHandlers>();

        // Register security policies
        services.AddSingleton<ISecurityPolicy, NetworkGuardPolicy>();
        services.AddSingleton<ISecurityPolicy, ReputationPolicy>();
        services.AddSingleton<ISecurityPolicy, ConsensusPolicy>();
        services.AddSingleton<ISecurityPolicy, ContentSafetyPolicy>();
        services.AddSingleton<ISecurityPolicy, HoneypotPolicy>();
        services.AddSingleton<ISecurityPolicy, NatAbuseDetectionPolicy>();

        // Register security policy engine (composite)
        services.AddSingleton<slskd.Security.ISecurityPolicyEngine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<slskd.Security.CompositeSecurityPolicy>>();
            var policies = sp.GetServices<slskd.Security.ISecurityPolicy>();
            return new slskd.Security.CompositeSecurityPolicy(logger, policies);
        });

        return services;
    }

    /// <summary>
    /// Initialize signal system by registering channel handlers and starting receivers.
    /// </summary>
    public static async Task InitializeSignalSystemAsync(
        this IServiceProvider serviceProvider,
        string localPeerId,
        CancellationToken cancellationToken = default)
    {
        var signalBus = serviceProvider.GetRequiredService<ISignalBus>();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<SignalSystemOptions>>().CurrentValue;

        if (!options.Enabled)
        {
            return; // Signal system disabled
        }

        // Register Mesh channel handler if enabled
        if (options.MeshChannel.Enabled)
        {
            var meshSender = serviceProvider.GetService<IMeshMessageSender>();
            if (meshSender != null)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MeshSignalChannelHandler>>();
                var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<SignalSystemOptions>>();
                var meshHandler = new MeshSignalChannelHandler(logger, optionsMonitor, meshSender, localPeerId);
                signalBus.RegisterChannelHandler(SignalChannel.Mesh, meshHandler);
            }
        }

        // Register BT extension channel handler if enabled
        if (options.BtExtensionChannel.Enabled)
        {
            var btSender = serviceProvider.GetService<IBtExtensionSender>();
            if (btSender != null)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<BtExtensionSignalChannelHandler>>();
                var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<SignalSystemOptions>>();
                var btHandler = new BtExtensionSignalChannelHandler(logger, optionsMonitor, btSender, localPeerId);
                signalBus.RegisterChannelHandler(SignalChannel.BtExtension, btHandler);
            }
        }

        // Initialize Swarm signal handlers
        var swarmHandlers = serviceProvider.GetRequiredService<SwarmSignalHandlers>();
        await swarmHandlers.InitializeAsync(cancellationToken);
    }
}















