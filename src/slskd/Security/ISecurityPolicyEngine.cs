namespace slskd.Security;

public record SecurityContext(string PeerId, string? ContentId = null, string? Operation = null);
public record SecurityDecision(bool Allowed, string Reason = "");

/// <summary>
/// Evaluates security policies for mesh operations.
/// </summary>
public interface ISecurityPolicyEngine
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default);
}

/// <summary>
/// Security policy abstraction.
/// </summary>
public interface ISecurityPolicy
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct = default);
}
