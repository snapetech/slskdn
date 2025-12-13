// <copyright file="IdentityKeyStoreTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Security;
using Xunit;

public class IdentityKeyStoreTests
{
    [Fact]
    public void ComputePeerId_IsDeterministic()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var logger = new Mock<ILogger<FileIdentityKeyStore>>();
            var store = new FileIdentityKeyStore(logger.Object, tempFile);

            // Act
            var peerId1 = store.ComputePeerId();
            var peerId2 = store.ComputePeerId();

            // Assert
            Assert.Equal(peerId1, peerId2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PeerId_MatchesExpectedFormat()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var logger = new Mock<ILogger<FileIdentityKeyStore>>();
            var store = new FileIdentityKeyStore(logger.Object, tempFile);

            // Act
            var peerId = store.ComputePeerId();

            // Assert - should be 64 hex characters (SHA256 hash)
            Assert.Matches("^[a-f0-9]{64}$", peerId);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Keys_PersistAcrossInstances()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var logger = new Mock<ILogger<FileIdentityKeyStore>>();
            
            // Act - create first instance
            var store1 = new FileIdentityKeyStore(logger.Object, tempFile);
            var peerId1 = store1.ComputePeerId();
            var publicKey1 = store1.PublicKey;

            // Act - create second instance with same file
            var store2 = new FileIdentityKeyStore(logger.Object, tempFile);
            var peerId2 = store2.ComputePeerId();
            var publicKey2 = store2.PublicKey;

            // Assert - should be identical
            Assert.Equal(peerId1, peerId2);
            Assert.Equal(publicKey1, publicKey2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Sign_ProducesValidSignature()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var logger = new Mock<ILogger<FileIdentityKeyStore>>();
            var store = new FileIdentityKeyStore(logger.Object, tempFile);
            var data = System.Text.Encoding.UTF8.GetBytes("test message");

            // Act
            var signature = store.Sign(data);

            // Assert - Ed25519 signature is 64 bytes
            Assert.Equal(64, signature.Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PeerId_DerivedFromPublicKey()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var logger = new Mock<ILogger<FileIdentityKeyStore>>();
            var store = new FileIdentityKeyStore(logger.Object, tempFile);

            // Act
            var computedPeerId = store.ComputePeerId();
            var expectedPeerId = Convert.ToHexString(SHA256.HashData(store.PublicKey)).ToLowerInvariant();

            // Assert
            Assert.Equal(expectedPeerId, computedPeerId);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

