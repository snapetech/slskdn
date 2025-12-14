// <copyright file="TransferSecurity.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides security integration for file transfers.
/// Combines PathGuard, ContentSafety, and related components.
/// </summary>
public sealed class TransferSecurity
{
    private readonly ILogger<TransferSecurity> _logger;
    private readonly ISecurityEventSink? _eventSink;
    private readonly ViolationTracker? _violationTracker;
    private readonly PeerReputation? _peerReputation;
    private readonly TemporalConsistency? _temporalConsistency;
    private readonly Honeypot? _honeypot;

    /// <summary>
    /// Root directory for downloads.
    /// </summary>
    public string DownloadRoot { get; set; } = string.Empty;

    /// <summary>
    /// Root directory for shares.
    /// </summary>
    public string ShareRoot { get; set; } = string.Empty;

    /// <summary>
    /// Whether to quarantine suspicious files.
    /// </summary>
    public bool QuarantineSuspicious { get; set; } = true;

    /// <summary>
    /// Quarantine directory path.
    /// </summary>
    public string QuarantineDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferSecurity"/> class.
    /// </summary>
    public TransferSecurity(
        ILogger<TransferSecurity> logger,
        ISecurityEventSink? eventSink = null,
        ViolationTracker? violationTracker = null,
        PeerReputation? peerReputation = null,
        TemporalConsistency? temporalConsistency = null,
        Honeypot? honeypot = null)
    {
        _logger = logger;
        _eventSink = eventSink;
        _violationTracker = violationTracker;
        _peerReputation = peerReputation;
        _temporalConsistency = temporalConsistency;
        _honeypot = honeypot;
    }

    /// <summary>
    /// Validate a download path from a peer.
    /// </summary>
    /// <param name="peerPath">The path received from the peer.</param>
    /// <param name="username">The peer's username.</param>
    /// <returns>Validation result with safe path if valid.</returns>
    public DownloadPathValidation ValidateDownloadPath(string peerPath, string username)
    {
        if (string.IsNullOrWhiteSpace(DownloadRoot))
        {
            return DownloadPathValidation.Failed("Download root not configured");
        }

        var validation = PathGuard.Validate(peerPath, DownloadRoot);

        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Invalid download path from {Username}: {Path} - {Error}",
                username, peerPath, validation.Error);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.PathTraversal,
                validation.ViolationType == PathViolationType.DirectoryTraversal
                    ? SecuritySeverity.High
                    : SecuritySeverity.Medium,
                $"Invalid download path: {validation.Error}",
                username: username));

            if (validation.ViolationType == PathViolationType.DirectoryTraversal)
            {
                _violationTracker?.RecordUsernameViolation(username, ViolationType.PathTraversal, peerPath);
                _peerReputation?.RecordProtocolViolation(username, "Path traversal attempt");
            }

            return DownloadPathValidation.Failed(validation.Error ?? "Invalid path");
        }

        // Check for dangerous extensions
        if (PathGuard.HasDangerousExtension(peerPath))
        {
            _logger.LogWarning(
                "Dangerous extension in download from {Username}: {Path}",
                username, peerPath);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.ContentSafety,
                SecuritySeverity.Medium,
                $"Dangerous extension: {Path.GetExtension(peerPath)}",
                username: username));

            return DownloadPathValidation.Warned(
                validation.SafePath!,
                "File has potentially dangerous extension");
        }

        return DownloadPathValidation.Succeeded(validation.SafePath!);
    }

    /// <summary>
    /// Validate a share path (path of file being served to a peer).
    /// </summary>
    /// <param name="requestedPath">The path requested by the peer.</param>
    /// <param name="username">The peer's username.</param>
    /// <returns>Validation result with safe path if valid.</returns>
    public SharePathValidation ValidateSharePath(string requestedPath, string username)
    {
        if (string.IsNullOrWhiteSpace(ShareRoot))
        {
            return SharePathValidation.Failed("Share root not configured");
        }

        // Check if this is a honeypot file
        if (_honeypot?.IsHoneypotFile(requestedPath) == true)
        {
            _logger.LogWarning(
                "Honeypot file requested by {Username}: {Path}",
                username, requestedPath);

            // Record but don't block - we want to see what they do
            _honeypot.RecordInteraction(
                System.Net.IPAddress.None, // We don't have IP here
                username,
                HoneypotAction.Download,
                requestedPath);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.Honeypot,
                SecuritySeverity.High,
                $"Honeypot file requested: {requestedPath}",
                username: username));

            // Return failed so we don't actually serve it
            return SharePathValidation.Failed("File not available");
        }

        var validation = PathGuard.Validate(requestedPath, ShareRoot);

        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Invalid share path request from {Username}: {Path} - {Error}",
                username, requestedPath, validation.Error);

            if (validation.ViolationType == PathViolationType.DirectoryTraversal)
            {
                _violationTracker?.RecordUsernameViolation(username, ViolationType.PathTraversal, requestedPath);
                _peerReputation?.RecordProtocolViolation(username, "Share path traversal");
            }

            return SharePathValidation.Failed(validation.Error ?? "Invalid path");
        }

        return SharePathValidation.Succeeded(validation.SafePath!);
    }

    /// <summary>
    /// Verify downloaded content safety.
    /// </summary>
    /// <param name="filePath">Path to the downloaded file.</param>
    /// <param name="username">Username who sent the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content verification result.</returns>
    public async Task<DownloadVerification> VerifyDownloadAsync(
        string filePath,
        string username,
        CancellationToken cancellationToken = default)
    {
        var result = await ContentSafety.VerifyFileAsync(filePath, cancellationToken);

        if (result.ThreatLevel == ContentThreatLevel.Dangerous)
        {
            _logger.LogError(
                "DANGEROUS content detected in download from {Username}: {Path} - {Type}",
                username, filePath, result.DetectedType);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.ContentSafety,
                SecuritySeverity.Critical,
                $"Dangerous content: {result.Message}",
                username: username));

            _violationTracker?.RecordUsernameViolation(username, ViolationType.DangerousContent, result.DetectedType);
            _peerReputation?.RecordContentMismatch(username, result.Message ?? "Dangerous content");

            // Quarantine the file
            if (QuarantineSuspicious && !string.IsNullOrEmpty(QuarantineDirectory))
            {
                var quarantinePath = await QuarantineFileAsync(filePath, username, result.Message);
                return DownloadVerification.Quarantined(result.Message ?? "Dangerous content", quarantinePath);
            }

            return DownloadVerification.Dangerous(result.Message ?? "Dangerous content detected");
        }

        if (result.ThreatLevel == ContentThreatLevel.Mismatch)
        {
            _logger.LogWarning(
                "Content mismatch in download from {Username}: {Path} - {Type}",
                username, filePath, result.DetectedType);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.ContentSafety,
                SecuritySeverity.Medium,
                $"Content mismatch: {result.Message}",
                username: username));

            _peerReputation?.RecordContentMismatch(username, result.Message ?? "Content mismatch");

            return DownloadVerification.Warned(result.Message ?? "Content mismatch");
        }

        if (result.IsWarning)
        {
            return DownloadVerification.Warned(result.Message ?? "Content warning");
        }

        // Record successful transfer for reputation
        var fileInfo = new FileInfo(filePath);
        _peerReputation?.RecordSuccessfulTransfer(username, fileInfo.Length);

        return DownloadVerification.Verified();
    }

    /// <summary>
    /// Record file metadata for temporal consistency tracking.
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <param name="filename">Filename.</param>
    /// <param name="size">File size.</param>
    /// <param name="hash">File hash if known.</param>
    /// <param name="bitrate">Bitrate if audio.</param>
    /// <param name="duration">Duration if audio.</param>
    /// <returns>Consistency analysis result.</returns>
    public ConsistencyAnalysis? RecordFileMetadata(
        string username,
        string filename,
        long size,
        string? hash = null,
        int bitrate = 0,
        int duration = 0)
    {
        if (_temporalConsistency == null)
        {
            return null;
        }

        var metadata = new FileMetadata
        {
            Size = size,
            Hash = hash,
            Bitrate = bitrate,
            Duration = duration,
        };

        var analysis = _temporalConsistency.RecordMetadata(username, filename, metadata);

        if (analysis.IsSuspicious)
        {
            _logger.LogWarning(
                "Suspicious metadata pattern from {Username} for {File}: {Issues}",
                username, filename, string.Join("; ", analysis.Issues));

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.Verification,
                SecuritySeverity.Medium,
                $"Suspicious metadata: {string.Join("; ", analysis.Issues)}",
                username: username));
        }

        return analysis;
    }

    /// <summary>
    /// Quarantine a suspicious file.
    /// </summary>
    private async Task<string?> QuarantineFileAsync(string filePath, string username, string? reason)
    {
        try
        {
            if (string.IsNullOrEmpty(QuarantineDirectory))
            {
                return null;
            }

            Directory.CreateDirectory(QuarantineDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeUsername = PathGuard.SanitizeFilename(username);
            var originalName = Path.GetFileName(filePath);
            var quarantineName = $"{timestamp}_{safeUsername}_{originalName}.quarantine";
            var quarantinePath = Path.Combine(QuarantineDirectory, quarantineName);

            await Task.Run(() => File.Move(filePath, quarantinePath));

            // Write metadata file
            var metadataPath = quarantinePath + ".meta";
            var metadata = $"Original: {filePath}\nUsername: {username}\nReason: {reason}\nTime: {DateTime.UtcNow:O}";
            await File.WriteAllTextAsync(metadataPath, metadata);

            _logger.LogInformation("File quarantined: {Original} -> {Quarantine}", filePath, quarantinePath);

            return quarantinePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine file: {SanitizedPath}", LoggingSanitizer.SanitizeFilePath(filePath));
            return null;
        }
    }
}

/// <summary>
/// Result of download path validation.
/// </summary>
public sealed class DownloadPathValidation
{
    /// <summary>Gets whether valid.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets whether warned.</summary>
    public bool IsWarning { get; init; }

    /// <summary>Gets the safe path if valid.</summary>
    public string? SafePath { get; init; }

    /// <summary>Gets any error or warning message.</summary>
    public string? Message { get; init; }

    /// <summary>Create success result.</summary>
    public static DownloadPathValidation Succeeded(string safePath) => new()
    {
        IsValid = true,
        SafePath = safePath,
    };

    /// <summary>Create warning result.</summary>
    public static DownloadPathValidation Warned(string safePath, string message) => new()
    {
        IsValid = true,
        IsWarning = true,
        SafePath = safePath,
        Message = message,
    };

    /// <summary>Create failure result.</summary>
    public static DownloadPathValidation Failed(string message) => new()
    {
        IsValid = false,
        Message = message,
    };
}

/// <summary>
/// Result of share path validation.
/// </summary>
public sealed class SharePathValidation
{
    /// <summary>Gets whether valid.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the safe path if valid.</summary>
    public string? SafePath { get; init; }

    /// <summary>Gets any error message.</summary>
    public string? Error { get; init; }

    /// <summary>Create success result.</summary>
    public static SharePathValidation Succeeded(string safePath) => new()
    {
        IsValid = true,
        SafePath = safePath,
    };

    /// <summary>Create failure result.</summary>
    public static SharePathValidation Failed(string error) => new()
    {
        IsValid = false,
        Error = error,
    };
}

/// <summary>
/// Result of download content verification.
/// </summary>
public sealed class DownloadVerification
{
    /// <summary>Gets whether verified safe.</summary>
    public bool IsVerified { get; init; }

    /// <summary>Gets whether warned.</summary>
    public bool IsWarning { get; init; }

    /// <summary>Gets whether dangerous.</summary>
    public bool IsDangerous { get; init; }

    /// <summary>Gets whether quarantined.</summary>
    public bool IsQuarantined { get; init; }

    /// <summary>Gets any message.</summary>
    public string? Message { get; init; }

    /// <summary>Gets quarantine path if quarantined.</summary>
    public string? QuarantinePath { get; init; }

    /// <summary>Create verified result.</summary>
    public static DownloadVerification Verified() => new() { IsVerified = true };

    /// <summary>Create warning result.</summary>
    public static DownloadVerification Warned(string message) => new()
    {
        IsVerified = true,
        IsWarning = true,
        Message = message,
    };

    /// <summary>Create dangerous result.</summary>
    public static DownloadVerification Dangerous(string message) => new()
    {
        IsVerified = false,
        IsDangerous = true,
        Message = message,
    };

    /// <summary>Create quarantined result.</summary>
    public static DownloadVerification Quarantined(string message, string? path) => new()
    {
        IsVerified = false,
        IsDangerous = true,
        IsQuarantined = true,
        Message = message,
        QuarantinePath = path,
    };
}


