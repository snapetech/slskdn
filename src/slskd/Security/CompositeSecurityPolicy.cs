using Microsoft.Extensions.Logging;

namespace slskd.Security;

/// <summary>
/// Composite policy that short-circuits on deny.
/// </summary>
public class CompositeSecurityPolicy : ISecurityPolicyEngine
{
    private readonly ILogger<CompositeSecurityPolicy> logger;
    private readonly IReadOnlyList<ISecurityPolicy> policies;

    public CompositeSecurityPolicy(ILogger<CompositeSecurityPolicy> logger, IEnumerable<ISecurityPolicy> policies)
    {
        this.logger = logger;
        this.policies = policies.ToList();
    }

    public async Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        foreach (var policy in policies)
        {
            var decision = await policy.EvaluateAsync(context, ct);
            if (!decision.Allowed)
            {
                logger.LogWarning("[Security] Deny {PeerId} {Op}: {Reason}", context.PeerId, context.Operation, decision.Reason);
                return decision;
            }
        }

        return new SecurityDecision(true, "all policies passed");
    }
}
















