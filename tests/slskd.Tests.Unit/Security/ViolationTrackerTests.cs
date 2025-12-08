// <copyright file="ViolationTrackerTests.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Security;

using System;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Common.Security;
using Xunit;

public class ViolationTrackerTests
{
    private readonly ViolationTracker _tracker;
    private readonly IPAddress _testIp = IPAddress.Parse("192.168.1.100");

    public ViolationTrackerTests()
    {
        _tracker = new ViolationTracker(NullLogger<ViolationTracker>.Instance)
        {
            ViolationWindow = TimeSpan.FromMinutes(1),
            ViolationsBeforeAutoBan = 3,
            AutoBansBeforePermanent = 2,
            BaseBanDuration = TimeSpan.FromMinutes(1),
        };
    }

    [Fact]
    public void RecordIpViolation_SingleViolation_ReturnsWarningOrNone()
    {
        var action = _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        
        // With threshold of 3, a single violation may trigger warning (50% = 1.5 rounds to 1)
        Assert.True(action == ViolationAction.None || action == ViolationAction.Warning);
        Assert.False(_tracker.IsIpBanned(_testIp));
    }

    [Fact]
    public void RecordIpViolation_MultipleViolations_TriggersWarning()
    {
        // First violation
        _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        
        // Second violation (50% of threshold)
        var action = _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        
        Assert.Equal(ViolationAction.Warning, action);
    }

    [Fact]
    public void RecordIpViolation_ThresholdReached_TriggersBan()
    {
        for (int i = 0; i < 3; i++)
        {
            _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        }
        
        Assert.True(_tracker.IsIpBanned(_testIp));
    }

    [Fact]
    public void RecordUsernameViolation_ThresholdReached_TriggersBan()
    {
        var username = "baduser";
        
        for (int i = 0; i < 3; i++)
        {
            _tracker.RecordUsernameViolation(username, ViolationType.ProtocolViolation);
        }
        
        Assert.True(_tracker.IsUsernameBanned(username));
    }

    [Fact]
    public void IsIpBanned_NotBanned_ReturnsFalse()
    {
        Assert.False(_tracker.IsIpBanned(IPAddress.Parse("10.0.0.1")));
    }

    [Fact]
    public void IsUsernameBanned_NotBanned_ReturnsFalse()
    {
        Assert.False(_tracker.IsUsernameBanned("gooduser"));
    }

    [Fact]
    public void BanIp_ManualBan_IsBanned()
    {
        var ip = IPAddress.Parse("10.0.0.50");
        
        _tracker.BanIp(ip, "Test ban", TimeSpan.FromHours(1));
        
        Assert.True(_tracker.IsIpBanned(ip));
    }

    [Fact]
    public void BanUsername_ManualBan_IsBanned()
    {
        var username = "testuser";
        
        _tracker.BanUsername(username, "Test ban", TimeSpan.FromHours(1));
        
        Assert.True(_tracker.IsUsernameBanned(username));
    }

    [Fact]
    public void UnbanIp_AfterBan_IsNotBanned()
    {
        var ip = IPAddress.Parse("10.0.0.51");
        
        _tracker.BanIp(ip, "Test ban");
        Assert.True(_tracker.IsIpBanned(ip));
        
        var result = _tracker.UnbanIp(ip);
        Assert.True(result);
        Assert.False(_tracker.IsIpBanned(ip));
    }

    [Fact]
    public void UnbanUsername_AfterBan_IsNotBanned()
    {
        var username = "testuser2";
        
        _tracker.BanUsername(username, "Test ban");
        Assert.True(_tracker.IsUsernameBanned(username));
        
        var result = _tracker.UnbanUsername(username);
        Assert.True(result);
        Assert.False(_tracker.IsUsernameBanned(username));
    }

    [Fact]
    public void GetIpViolations_ReturnsRecord()
    {
        _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        
        var record = _tracker.GetIpViolations(_testIp);
        
        Assert.NotNull(record);
        Assert.Equal(1, record.TotalViolations);
    }

    [Fact]
    public void GetUsernameViolations_ReturnsRecord()
    {
        var username = "trackeduser";
        _tracker.RecordUsernameViolation(username, ViolationType.Abuse);
        
        var record = _tracker.GetUsernameViolations(username);
        
        Assert.NotNull(record);
        Assert.Equal(1, record.TotalViolations);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        _tracker.RecordIpViolation(_testIp, ViolationType.InvalidMessage);
        _tracker.RecordUsernameViolation("user1", ViolationType.Abuse);
        
        var stats = _tracker.GetStats();
        
        Assert.Equal(1, stats.TrackedIps);
        Assert.Equal(1, stats.TrackedUsernames);
    }

    [Fact]
    public void GetActiveBans_ReturnsOnlyActive()
    {
        _tracker.BanIp(IPAddress.Parse("10.0.0.100"), "Ban 1");
        _tracker.BanUsername("banneduser", "Ban 2");
        
        var bans = _tracker.GetActiveBans();
        
        Assert.Equal(2, bans.Count);
    }

    [Fact]
    public void GetIpBan_ReturnsBanRecord()
    {
        var ip = IPAddress.Parse("10.0.0.101");
        _tracker.BanIp(ip, "Test reason", TimeSpan.FromHours(1));
        
        var ban = _tracker.GetIpBan(ip);
        
        Assert.NotNull(ban);
        Assert.Contains("Test reason", ban.Reason);
    }

    [Fact]
    public void GetUsernameBan_ReturnsBanRecord()
    {
        var username = "banneduser2";
        _tracker.BanUsername(username, "Test reason");
        
        var ban = _tracker.GetUsernameBan(username);
        
        Assert.NotNull(ban);
        Assert.Contains("Test reason", ban.Reason);
    }

    [Fact]
    public void RecordIpViolation_EmptyUsername_NoException()
    {
        // Should handle gracefully
        var action = _tracker.RecordUsernameViolation("", ViolationType.InvalidMessage);
        Assert.Equal(ViolationAction.None, action);
    }

    [Fact]
    public void IsUsernameBanned_CaseInsensitive()
    {
        _tracker.BanUsername("TestUser", "Ban");
        
        Assert.True(_tracker.IsUsernameBanned("testuser"));
        Assert.True(_tracker.IsUsernameBanned("TESTUSER"));
    }
}

