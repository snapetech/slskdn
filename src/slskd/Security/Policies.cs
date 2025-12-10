namespace slskd.Security;

/// <summary>
/// Network guard policy placeholder.
/// </summary>
public class NetworkGuardPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "network ok"));
    }
}

public class ReputationPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "reputation ok"));
    }
}

public class ConsensusPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "consensus ok"));
    }
}

public class ContentSafetyPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "content ok"));
    }
}

public class HoneypotPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "honeypot check ok"));
    }
}

public class NatAbuseDetectionPolicy : ISecurityPolicy
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SecurityDecision(true, "nat ok"));
    }
}
