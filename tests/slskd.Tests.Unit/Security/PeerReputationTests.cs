// <copyright file="PeerReputationTests.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Security;

using Microsoft.Extensions.Logging.Abstractions;
using slskd.Common.Security;
using Xunit;

public class PeerReputationTests
{
    private readonly PeerReputation _reputation;

    public PeerReputationTests()
    {
        _reputation = new PeerReputation(NullLogger<PeerReputation>.Instance);
    }

    [Fact]
    public void GetOrCreateProfile_NewUser_ReturnsBaseScore()
    {
        var profile = _reputation.GetOrCreateProfile("newuser");
        
        Assert.NotNull(profile);
        Assert.Equal(PeerReputation.BaseScore, profile.Score);
        Assert.Equal("newuser", profile.Username);
    }

    [Fact]
    public void GetScore_UnknownUser_ReturnsNull()
    {
        var score = _reputation.GetScore("unknownuser");
        Assert.Null(score);
    }

    [Fact]
    public void GetScore_KnownUser_ReturnsScore()
    {
        _reputation.GetOrCreateProfile("knownuser");
        var score = _reputation.GetScore("knownuser");
        
        Assert.NotNull(score);
        Assert.Equal(PeerReputation.BaseScore, score);
    }

    [Fact]
    public void RecordSuccessfulTransfer_IncreasesScore()
    {
        var profile = _reputation.GetOrCreateProfile("goodpeer");
        var initialScore = profile.Score;
        
        _reputation.RecordSuccessfulTransfer("goodpeer", 1000);
        
        Assert.True(profile.Score > initialScore);
        Assert.Equal(1, profile.SuccessfulTransfers);
        Assert.Equal(1000, profile.TotalBytesTransferred);
    }

    [Fact]
    public void RecordFailedTransfer_DecreasesScore()
    {
        var profile = _reputation.GetOrCreateProfile("badpeer");
        var initialScore = profile.Score;
        
        _reputation.RecordFailedTransfer("badpeer", "timeout");
        
        Assert.True(profile.Score < initialScore);
        Assert.Equal(1, profile.FailedTransfers);
    }

    [Fact]
    public void RecordMalformedMessage_DecreasesScore()
    {
        var profile = _reputation.GetOrCreateProfile("malicious");
        var initialScore = profile.Score;
        
        _reputation.RecordMalformedMessage("malicious");
        
        Assert.True(profile.Score < initialScore);
        Assert.Equal(1, profile.MalformedMessages);
    }

    [Fact]
    public void RecordProtocolViolation_DecreasesScoreSignificantly()
    {
        var profile = _reputation.GetOrCreateProfile("violator");
        var initialScore = profile.Score;
        
        _reputation.RecordProtocolViolation("violator", "bad handshake");
        
        Assert.True(profile.Score < initialScore - 10);
        Assert.Equal(1, profile.ProtocolViolations);
    }

    [Fact]
    public void RecordContentMismatch_DecreasesScore()
    {
        var profile = _reputation.GetOrCreateProfile("faker");
        var initialScore = profile.Score;
        
        _reputation.RecordContentMismatch("faker", "wrong hash");
        
        Assert.True(profile.Score < initialScore);
        Assert.Equal(1, profile.ContentMismatches);
    }

    [Fact]
    public void IsTrusted_HighScore_ReturnsTrue()
    {
        _reputation.SetScore("trusted", 80, "test");
        Assert.True(_reputation.IsTrusted("trusted"));
    }

    [Fact]
    public void IsTrusted_LowScore_ReturnsFalse()
    {
        _reputation.SetScore("untrusted", 30, "test");
        Assert.False(_reputation.IsTrusted("untrusted"));
    }

    [Fact]
    public void IsUntrusted_LowScore_ReturnsTrue()
    {
        _reputation.SetScore("bad", 10, "test");
        Assert.True(_reputation.IsUntrusted("bad"));
    }

    [Fact]
    public void IsUntrusted_HighScore_ReturnsFalse()
    {
        _reputation.SetScore("good", 80, "test");
        Assert.False(_reputation.IsUntrusted("good"));
    }

    [Fact]
    public void SetScore_SetsExactScore()
    {
        _reputation.SetScore("testuser", 75, "manual set");
        
        var score = _reputation.GetScore("testuser");
        Assert.Equal(75, score);
    }

    [Fact]
    public void SetScore_ClampsToMax()
    {
        _reputation.SetScore("testuser", 150, "above max");
        
        var score = _reputation.GetScore("testuser");
        Assert.Equal(PeerReputation.MaxScore, score);
    }

    [Fact]
    public void SetScore_ClampsToMin()
    {
        _reputation.SetScore("testuser", -50, "below min");
        
        var score = _reputation.GetScore("testuser");
        Assert.Equal(PeerReputation.MinScore, score);
    }

    [Fact]
    public void GetSuspiciousPeers_ReturnsLowScorePeers()
    {
        _reputation.SetScore("suspicious1", 30, "test");
        _reputation.SetScore("suspicious2", 25, "test");
        _reputation.SetScore("good", 80, "test");
        
        var suspicious = _reputation.GetSuspiciousPeers();
        
        Assert.Equal(2, suspicious.Count);
        Assert.All(suspicious, p => Assert.True(p.Score < PeerReputation.BaseScore));
    }

    [Fact]
    public void GetTrustedPeers_ReturnsHighScorePeers()
    {
        _reputation.SetScore("trusted1", 80, "test");
        _reputation.SetScore("trusted2", 90, "test");
        _reputation.SetScore("neutral", 50, "test");
        
        var trusted = _reputation.GetTrustedPeers();
        
        Assert.Equal(2, trusted.Count);
        Assert.All(trusted, p => Assert.True(p.Score >= PeerReputation.TrustedThreshold));
    }

    [Fact]
    public void RankByReputation_SortsByScoreDescending()
    {
        _reputation.SetScore("user1", 30, "test");
        _reputation.SetScore("user2", 90, "test");
        _reputation.SetScore("user3", 60, "test");
        
        var ranked = _reputation.RankByReputation(new[] { "user1", "user2", "user3" });
        
        Assert.Equal("user2", ranked[0]);
        Assert.Equal("user3", ranked[1]);
        Assert.Equal("user1", ranked[2]);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        _reputation.SetScore("trusted", 80, "test");
        _reputation.SetScore("untrusted", 10, "test");
        _reputation.SetScore("neutral", 50, "test");
        
        var stats = _reputation.GetStats();
        
        Assert.Equal(3, stats.TotalPeers);
        Assert.Equal(1, stats.TrustedPeers);
        Assert.Equal(1, stats.UntrustedPeers);
    }

    [Fact]
    public void SuccessRate_CalculatesCorrectly()
    {
        var profile = _reputation.GetOrCreateProfile("testpeer");
        
        _reputation.RecordSuccessfulTransfer("testpeer", 100);
        _reputation.RecordSuccessfulTransfer("testpeer", 100);
        _reputation.RecordFailedTransfer("testpeer");
        
        Assert.Equal(2.0 / 3.0, profile.SuccessRate, 2);
    }

    [Fact]
    public void TrustLevel_ReflectsScore()
    {
        var profile = _reputation.GetOrCreateProfile("testpeer");
        
        _reputation.SetScore("testpeer", 80, "test");
        Assert.Equal(TrustLevel.Trusted, profile.TrustLevel);
        
        _reputation.SetScore("testpeer", 10, "test");
        Assert.Equal(TrustLevel.Untrusted, profile.TrustLevel);
        
        _reputation.SetScore("testpeer", 50, "test");
        Assert.Equal(TrustLevel.Neutral, profile.TrustLevel);
    }
}

