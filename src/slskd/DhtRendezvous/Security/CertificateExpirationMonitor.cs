// <copyright file="CertificateExpirationMonitor.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Monitors certificate expiration and logs warnings when certificates approach expiry.
/// </summary>
public class CertificateExpirationMonitor : BackgroundService
{
    private readonly CertificateManager _certificateManager;
    private readonly ILogger<CertificateExpirationMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);
    
    // Warning thresholds
    private static readonly TimeSpan CriticalThreshold = TimeSpan.FromDays(7);
    private static readonly TimeSpan WarningThreshold = TimeSpan.FromDays(30);
    
    public CertificateExpirationMonitor(
        CertificateManager certificateManager,
        ILogger<CertificateExpirationMonitor> logger)
    {
        _certificateManager = certificateManager;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CertExpMonitor] Certificate expiration monitoring started (check interval: {Interval})", _checkInterval);
        
        // Check immediately on startup
        await CheckCertificateExpirationAsync();
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await CheckCertificateExpirationAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[CertExpMonitor] Certificate expiration monitoring stopped");
        }
    }
    
    private async Task CheckCertificateExpirationAsync()
    {
        try
        {
            var serverCert = _certificateManager.GetOrCreateServerCertificate();
            
            await Task.Run(() => CheckCertificate(serverCert, "Server"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CertExpMonitor] Failed to check certificate expiration");
        }
    }
    
    private void CheckCertificate(X509Certificate2 certificate, string certType)
    {
        var now = DateTime.UtcNow;
        var expiresAt = certificate.NotAfter.ToUniversalTime();
        var timeUntilExpiry = expiresAt - now;
        
        if (timeUntilExpiry < TimeSpan.Zero)
        {
            _logger.LogCritical(
                "[CertExpMonitor] 🔴 {Type} certificate has EXPIRED on {ExpiryDate}! " +
                "TLS connections will fail. Regenerate certificate immediately.",
                certType,
                expiresAt);
        }
        else if (timeUntilExpiry < CriticalThreshold)
        {
            _logger.LogCritical(
                "[CertExpMonitor] 🔴 {Type} certificate expires in {Days} days ({ExpiryDate})! " +
                "Regenerate certificate urgently to avoid service disruption.",
                certType,
                Math.Ceiling(timeUntilExpiry.TotalDays),
                expiresAt);
        }
        else if (timeUntilExpiry < WarningThreshold)
        {
            _logger.LogWarning(
                "[CertExpMonitor] ⚠️ {Type} certificate expires in {Days} days ({ExpiryDate}). " +
                "Consider regenerating certificate soon.",
                certType,
                Math.Ceiling(timeUntilExpiry.TotalDays),
                expiresAt);
        }
        else
        {
            _logger.LogDebug(
                "[CertExpMonitor] ✓ {Type} certificate is valid until {ExpiryDate} ({Days} days)",
                certType,
                expiresAt,
                Math.Ceiling(timeUntilExpiry.TotalDays));
        }
    }
}

