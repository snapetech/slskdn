// <copyright file="DescriptorSignerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Dht;
using slskd.Mesh.Security;
using Xunit;

public class DescriptorSignerTests
{
    [Fact]
    public void Verify_AcceptsValidSignature()
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        
        var (identityStore, descriptor) = CreateSignedDescriptor();

        // Act
        var result = signer.Verify(descriptor);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_RejectsTamperedDescriptor()
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        
        var (identityStore, descriptor) = CreateSignedDescriptor();

        // Tamper with the descriptor
        descriptor.Endpoints.Add("udp://evil.com:50400");

        // Act
        var result = signer.Verify(descriptor);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_RejectsPeerIdMismatch()
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        
        var (identityStore, descriptor) = CreateSignedDescriptor();

        // Change PeerId to not match identity public key
        descriptor.PeerId = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = signer.Verify(descriptor);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_RejectsMissingSignature()
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        
        var (identityStore, descriptor) = CreateSignedDescriptor();
        descriptor.Signature = string.Empty;

        // Act
        var result = signer.Verify(descriptor);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Sign_ProducesVerifiableSignature()
    {
        // Arrange
        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        
        var identityStore = CreateIdentityStore();
        var descriptor = new MeshPeerDescriptor
        {
            PeerId = identityStore.ComputePeerId(),
            Endpoints = new List<string> { "udp://localhost:50400" },
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IdentityPublicKey = Convert.ToBase64String(identityStore.PublicKey),
            TlsControlSpkiSha256 = "test",
            TlsDataSpkiSha256 = "test",
            ControlSigningPublicKeys = new List<string>(),
        };

        // Act
        signer.Sign(descriptor, identityStore.PrivateKey);

        // Assert
        Assert.NotEmpty(descriptor.Signature);
        Assert.True(signer.Verify(descriptor));
    }

    private static (IIdentityKeyStore store, MeshPeerDescriptor descriptor) CreateSignedDescriptor()
    {
        var identityStore = CreateIdentityStore();
        var descriptor = new MeshPeerDescriptor
        {
            PeerId = identityStore.ComputePeerId(),
            Endpoints = new List<string> { "udp://localhost:50400" },
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IdentityPublicKey = Convert.ToBase64String(identityStore.PublicKey),
            TlsControlSpkiSha256 = "test-control",
            TlsDataSpkiSha256 = "test-data",
            ControlSigningPublicKeys = new List<string>(),
        };

        var logger = new Mock<ILogger<DescriptorSigner>>();
        var signer = new DescriptorSigner(logger.Object);
        signer.Sign(descriptor, identityStore.PrivateKey);

        return (identityStore, descriptor);
    }

    private static IIdentityKeyStore CreateIdentityStore()
    {
        var tempFile = Path.GetTempFileName();
        var logger = new Mock<ILogger<FileIdentityKeyStore>>();
        return new FileIdentityKeyStore(logger.Object, tempFile);
    }
}

