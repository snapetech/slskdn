// <copyright file="CertificatePinValidationTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous.Security;
using Xunit;

/// <summary>
/// Tests for P0-1: Certificate Pin Validation (TOFU).
/// </summary>
public class CertificatePinValidationTests
{
    private readonly string _testAppDir;
    private readonly CertificateManager _certManager;
    private readonly CertificatePinStore _pinStore;

    public CertificatePinValidationTests()
    {
        _testAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(_testAppDir);
        
        _certManager = new CertificateManager(NullLogger<CertificateManager>.Instance, _testAppDir);
        _pinStore = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, _testAppDir);
    }

    [Fact]
    public void ValidateCertificatePin_FirstConnection_RecordsPin()
    {
        // Arrange
        var identifier = "192.168.1.100:50305";
        var cert = _certManager.GetOrCreateServerCertificate();

        // Act - First connection
        _pinStore.SetPin(identifier, cert.Thumbprint);
        var pinResult = _pinStore.CheckPin(identifier, cert.Thumbprint);

        // Assert
        pinResult.Should().Be(PinCheckResult.Valid, "first connection should record pin and validate on next check");
    }

    [Fact]
    public void ValidateCertificatePin_RepeatConnection_SameCert_Succeeds()
    {
        // Arrange
        var identifier = "192.168.1.100:50305";
        var cert = _certManager.GetOrCreateServerCertificate();

        // Act - Record pin
        _pinStore.SetPin(identifier, cert.Thumbprint);
        
        // Simulate repeat connection with same cert
        var pinResult = _pinStore.CheckPin(identifier, cert.Thumbprint);

        // Assert
        pinResult.Should().Be(PinCheckResult.Valid, "repeat connection with same cert should succeed");
    }

    [Fact]
    public void ValidateCertificatePin_RepeatConnection_DifferentCert_DetectsMismatch()
    {
        // Arrange
        var identifier = "192.168.1.100:50305";
        var cert1 = _certManager.GetOrCreateServerCertificate();

        // Record first cert
        _pinStore.SetPin(identifier, cert1.Thumbprint);

        // Simulate different cert thumbprint (MITM or cert rotation)
        var differentThumbprint = "DIFFERENT_THUMBPRINT_12345";

        // Act - Simulate repeat connection with different cert
        var pinResult = _pinStore.CheckPin(identifier, differentThumbprint);

        // Assert
        pinResult.Should().Be(PinCheckResult.Mismatch, "repeat connection with different cert should fail (TOFU violation)");
    }

    [Fact]
    public void CertificatePinStore_PersistsPins_AcrossInstances()
    {
        // Arrange
        var identifier = "192.168.1.100:50305";
        var cert = _certManager.GetOrCreateServerCertificate();

        // Act - Record pin
        _pinStore.SetPin(identifier, cert.Thumbprint);
        var firstResult = _pinStore.CheckPin(identifier, cert.Thumbprint);
        
        // Create new store instance (simulating restart)
        var newPinStore = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, _testAppDir);
        var secondResult = newPinStore.CheckPin(identifier, cert.Thumbprint);

        // Assert
        firstResult.Should().Be(PinCheckResult.Valid);
        secondResult.Should().Be(PinCheckResult.Valid, "pin should persist across restarts");
    }

    [Fact]
    public void ValidateCertificatePin_MultipleIdentifiers_TrackedIndependently()
    {
        // Arrange
        var identifier1 = "192.168.1.100:50305";
        var identifier2 = "192.168.1.101:50305";
        var cert1 = _certManager.GetOrCreateServerCertificate();
        
        // Simulate different cert for identifier2
        var cert2Thumbprint = "DIFFERENT_CERT_THUMBPRINT_67890";

        // Act
        _pinStore.SetPin(identifier1, cert1.Thumbprint);
        _pinStore.SetPin(identifier2, cert2Thumbprint);

        var result1 = _pinStore.CheckPin(identifier1, cert1.Thumbprint);
        var result2 = _pinStore.CheckPin(identifier2, cert2Thumbprint);

        // Assert
        result1.Should().Be(PinCheckResult.Valid);
        result2.Should().Be(PinCheckResult.Valid);
    }

    [Fact]
    public void ValidateCertificatePin_NeverSeen_ReturnsNotPinned()
    {
        // Arrange
        var identifier = "192.168.1.100:50305";
        var cert = _certManager.GetOrCreateServerCertificate();

        // Act
        var result = _pinStore.CheckPin(identifier, cert.Thumbprint);

        // Assert
        result.Should().Be(PinCheckResult.NotPinned, "never-seen identifier should return NotPinned");
    }
}

