// <copyright file="DescriptorSeqTrackerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using System.IO;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Security;
using Xunit;

public class DescriptorSeqTrackerTests : IDisposable
{
    private readonly string testFilePath;

    public DescriptorSeqTrackerTests()
    {
        testFilePath = Path.Combine(Path.GetTempPath(), $"test-seq-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(testFilePath))
        {
            File.Delete(testFilePath);
        }
    }

    [Theory]
    [AutoData]
    public void ValidateAndUpdate_FirstSeq_ReturnsTrue(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker = new DescriptorSeqTracker(logger.Object, testFilePath);

        // Act
        var result = tracker.ValidateAndUpdate(peerId, 1);

        // Assert
        result.Should().BeTrue();
        tracker.GetLastAcceptedSeq(peerId).Should().Be(1);
    }

    [Theory]
    [AutoData]
    public void ValidateAndUpdate_IncrementingSeq_ReturnsTrue(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker = new DescriptorSeqTracker(logger.Object, testFilePath);

        // Act & Assert
        tracker.ValidateAndUpdate(peerId, 1).Should().BeTrue();
        tracker.ValidateAndUpdate(peerId, 2).Should().BeTrue();
        tracker.ValidateAndUpdate(peerId, 3).Should().BeTrue();
        tracker.GetLastAcceptedSeq(peerId).Should().Be(3);
    }

    [Theory]
    [AutoData]
    public void ValidateAndUpdate_RollbackAttack_ReturnsFalse(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker = new DescriptorSeqTracker(logger.Object, testFilePath);

        tracker.ValidateAndUpdate(peerId, 10);

        // Act - try to roll back to seq 5
        var result = tracker.ValidateAndUpdate(peerId, 5);

        // Assert
        result.Should().BeFalse();
        tracker.GetLastAcceptedSeq(peerId).Should().Be(10); // Unchanged
    }

    [Theory]
    [AutoData]
    public void ValidateAndUpdate_SameSeq_ReturnsFalse(string peerId)
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker = new DescriptorSeqTracker(logger.Object, testFilePath);

        tracker.ValidateAndUpdate(peerId, 5);

        // Act - try to use same seq
        var result = tracker.ValidateAndUpdate(peerId, 5);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void ValidateAndUpdate_DifferentPeers_Independent(string peerId1, string peerId2)
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker = new DescriptorSeqTracker(logger.Object, testFilePath);

        // Act
        tracker.ValidateAndUpdate(peerId1, 10);
        tracker.ValidateAndUpdate(peerId2, 5);

        // Assert
        tracker.GetLastAcceptedSeq(peerId1).Should().Be(10);
        tracker.GetLastAcceptedSeq(peerId2).Should().Be(5);
    }

    [Theory]
    [AutoData]
    public void Persistence_SaveAndLoad_PreservesState(string peerId1, string peerId2)
    {
        // Arrange & Act
        var logger1 = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker1 = new DescriptorSeqTracker(logger1.Object, testFilePath);
        tracker1.ValidateAndUpdate(peerId1, 100);
        tracker1.ValidateAndUpdate(peerId2, 200);
        tracker1.Save();

        // Create new tracker with same file
        var logger2 = new Mock<ILogger<DescriptorSeqTracker>>();
        var tracker2 = new DescriptorSeqTracker(logger2.Object, testFilePath);

        // Assert
        tracker2.GetLastAcceptedSeq(peerId1).Should().Be(100);
        tracker2.GetLastAcceptedSeq(peerId2).Should().Be(200);
    }
}

