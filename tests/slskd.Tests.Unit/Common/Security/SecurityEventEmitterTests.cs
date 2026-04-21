namespace slskd.Tests.Unit.Common.Security;

using System;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Common.Security;
using Xunit;

public class SecurityEventEmitterTests
{
    [Fact]
    public void EntropyMonitor_Dispose_DisposesCheckTimer()
    {
        var monitor = new EntropyMonitor(NullLogger<EntropyMonitor>.Instance);
        var timer = GetTimerField(monitor, "_checkTimer");

        monitor.Dispose();

        Assert.False(timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void EntropyMonitor_UsesSampleSizeThatAvoidsRoutineEstimatorWarnings()
    {
        Assert.True(EntropyMonitor.SampleSize >= 4096);
        Assert.True(EntropyMonitor.WarningEntropy > EntropyMonitor.MinAcceptableEntropy);
        Assert.True(EntropyMonitor.WarningEntropy <= 7.75);
    }

    [Fact]
    public void SecurityEventAggregator_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        using var aggregator = new SecurityEventAggregator(NullLogger<SecurityEventAggregator>.Instance);
        var invokedHealthySubscriber = false;

        aggregator.HighSeverityEvent += (_, _) => throw new InvalidOperationException("boom");
        aggregator.HighSeverityEvent += (_, args) => invokedHealthySubscriber = args.Event.Severity == SecuritySeverity.High;

        aggregator.Report(new SecurityEvent
        {
            Type = SecurityEventType.Authentication,
            Severity = SecuritySeverity.High,
            Message = "test",
        });

        Assert.True(invokedHealthySubscriber);
    }

    [Fact]
    public void EntropyMonitor_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        using var monitor = new EntropyMonitor(NullLogger<EntropyMonitor>.Instance);
        var invokedHealthySubscriber = false;

        monitor.EntropyAlert += (_, _) => throw new InvalidOperationException("boom");
        monitor.EntropyAlert += (_, args) => invokedHealthySubscriber = args.Check.Status == EntropyStatus.Warning;

        var raiseMethod = typeof(EntropyMonitor).GetMethod(
            "RaiseEntropyAlert",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("EntropyMonitor.RaiseEntropyAlert method was not found.");

        raiseMethod.Invoke(monitor, [new EntropyCheck
        {
            Timestamp = DateTimeOffset.UtcNow,
            Entropy = 7.25,
            SampleSize = 256,
            ByteDistribution = new ByteDistribution
            {
                Frequencies = new int[256],
                ExpectedFrequency = 1,
                MaxFrequency = 1,
                MinFrequency = 0,
            },
            Status = EntropyStatus.Warning,
        }]);

        Assert.True(invokedHealthySubscriber);
    }

    [Fact]
    public void Honeypot_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        using var honeypot = new Honeypot(NullLogger<Honeypot>.Instance);
        var invokedHealthySubscriber = false;

        honeypot.HoneypotTriggered += (_, _) => throw new InvalidOperationException("boom");
        honeypot.HoneypotTriggered += (_, args) => invokedHealthySubscriber = args.Event.Interaction.Action == HoneypotAction.Download;

        honeypot.RecordInteraction(IPAddress.Parse("203.0.113.10"), "scanner", HoneypotAction.Download, "admin_credentials.txt");

        Assert.True(invokedHealthySubscriber);
    }

    [Fact]
    public void FingerprintDetection_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        using var detection = new FingerprintDetection(NullLogger<FingerprintDetection>.Instance);
        var invokedHealthySubscriber = false;
        var publicIp = IPAddress.Parse("203.0.113.20");

        detection.ReconnaissanceDetected += (_, _) => throw new InvalidOperationException("boom");
        detection.ReconnaissanceDetected += (_, args) => invokedHealthySubscriber = args.Event.IpAddress == publicIp.ToString();

        detection.RecordConnection(publicIp, 5000, protocolVersion: "v1", userAgent: "ua-1", succeeded: false);
        detection.RecordConnection(publicIp, 5001, protocolVersion: "v2", userAgent: "ua-2", succeeded: false);
        detection.RecordConnection(publicIp, 5002, protocolVersion: "v3", userAgent: "ua-3", succeeded: false);
        detection.RecordConnection(publicIp, 5003, protocolVersion: "v4", userAgent: "ua-4", succeeded: false);

        Assert.True(invokedHealthySubscriber);
    }

    private static System.Threading.Timer GetTimerField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{fieldName} field was not found.");

        return (System.Threading.Timer)field.GetValue(instance)!;
    }
}
