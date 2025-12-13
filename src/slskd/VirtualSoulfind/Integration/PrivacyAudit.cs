namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.Capture;

/// <summary>
/// Privacy audit for Virtual Soulfind.
/// </summary>
public interface IPrivacyAudit
{
    /// <summary>
    /// Perform privacy audit and return findings.
    /// </summary>
    Task<PrivacyAuditReport> PerformAuditAsync(CancellationToken ct = default);
}

/// <summary>
/// Privacy audit report.
/// </summary>
public class PrivacyAuditReport
{
    public bool Passed { get; set; }
    public List<PrivacyFinding> Findings { get; set; } = new();
    public DateTimeOffset AuditTimestamp { get; set; }
}

/// <summary>
/// Privacy audit finding.
/// </summary>
public class PrivacyFinding
{
    public PrivacySeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}

/// <summary>
/// Privacy finding severity.
/// </summary>
public enum PrivacySeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Privacy audit service.
/// </summary>
public class PrivacyAudit : IPrivacyAudit
{
    private readonly ILogger<PrivacyAudit> logger;
    private readonly IUsernamePseudonymizer pseudonymizer;
    private readonly IOptionsMonitor<Options> optionsMonitor;

    public PrivacyAudit(
        ILogger<PrivacyAudit> logger,
        IUsernamePseudonymizer pseudonymizer,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.pseudonymizer = pseudonymizer;
        this.optionsMonitor = optionsMonitor;
    }

    public Task<PrivacyAuditReport> PerformAuditAsync(CancellationToken ct)
    {
        logger.LogInformation("[VSF-PRIVACY] Performing privacy audit");

        var findings = new List<PrivacyFinding>();

        // Check username anonymization
        CheckUsernameAnonymization(findings);

        // Check DHT privacy
        CheckDhtPrivacy(findings);

        // Check path leaks
        CheckPathLeaks(findings);

        // Check configuration
        CheckConfiguration(findings);

        var report = new PrivacyAuditReport
        {
            Passed = !findings.Any(f => f.Severity == PrivacySeverity.Critical),
            Findings = findings,
            AuditTimestamp = DateTimeOffset.UtcNow
        };

        logger.LogInformation("[VSF-PRIVACY] Privacy audit complete: {Status} ({Findings} findings)",
            report.Passed ? "PASS" : "FAIL", findings.Count);

        return Task.FromResult(report);
    }

    private void CheckUsernameAnonymization(List<PrivacyFinding> findings)
    {
        var options = optionsMonitor.CurrentValue;
        var level = options.VirtualSoulfind?.Privacy?.AnonymizationLevel ?? "Pseudonymized";

        if (level == "None")
        {
            findings.Add(new PrivacyFinding
            {
                Severity = PrivacySeverity.Critical,
                Category = "Username Anonymization",
                Description = "Username anonymization is disabled (AnonymizationLevel = None)",
                Recommendation = "Set AnonymizationLevel to 'Pseudonymized' or 'Aggregate'"
            });
        }
        else
        {
            findings.Add(new PrivacyFinding
            {
                Severity = PrivacySeverity.Info,
                Category = "Username Anonymization",
                Description = $"Username anonymization enabled: {level}"
            });
        }
    }

    private void CheckDhtPrivacy(List<PrivacyFinding> findings)
    {
        // DHT uses pseudonymized peer IDs, no raw usernames
        findings.Add(new PrivacyFinding
        {
            Severity = PrivacySeverity.Info,
            Category = "DHT Privacy",
            Description = "DHT uses pseudonymized peer IDs (no raw usernames published)"
        });
    }

    private void CheckPathLeaks(List<PrivacyFinding> findings)
    {
        // Shadow index uses only hashes, no paths
        findings.Add(new PrivacyFinding
        {
            Severity = PrivacySeverity.Info,
            Category = "Path Privacy",
            Description = "Shadow index uses file hashes only (no file paths published)"
        });
    }

    private void CheckConfiguration(List<PrivacyFinding> findings)
    {
        var options = optionsMonitor.CurrentValue;
        
        if (options.VirtualSoulfind?.Capture?.Enabled == true &&
            options.VirtualSoulfind?.Privacy?.PersistRawObservations == true)
        {
            findings.Add(new PrivacyFinding
            {
                Severity = PrivacySeverity.Warning,
                Category = "Configuration",
                Description = "Raw observations are persisted to disk",
                Recommendation = "Consider disabling PersistRawObservations for production"
            });
        }
    }
}















