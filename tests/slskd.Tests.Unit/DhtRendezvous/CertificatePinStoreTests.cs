// <copyright file="CertificatePinStoreTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous.Security;
using Xunit;

public class CertificatePinStoreTests
{
    [Fact]
    public void CheckPin_WhenNoPinExists_ReturnsNotPinned()
    {
        using var tempDir = new TempDir();
        var store = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, tempDir.Path);

        var result = store.CheckPin("alice", "ABC123");

        Assert.Equal(PinCheckResult.NotPinned, result);
    }

    [Fact]
    public void RotatePin_WhenExistingPinDiffers_ReplacesThumbprint_AndPreservesFirstSeen()
    {
        using var tempDir = new TempDir();
        var store = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, tempDir.Path);
        store.SetPin("alice", "OLDPIN");

        var firstSeen = Assert.Single(store.GetAllPins()).FirstSeen;

        store.RotatePin("alice", "NEWPIN");

        var pin = Assert.Single(store.GetAllPins());
        Assert.Equal("NEWPIN", pin.Thumbprint);
        Assert.Equal(firstSeen, pin.FirstSeen);
        Assert.Equal(PinCheckResult.Valid, store.CheckPin("alice", "NEWPIN"));
    }

    [Fact]
    public void RotatePin_WhenPinMissing_AddsNewPin()
    {
        using var tempDir = new TempDir();
        var store = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, tempDir.Path);

        store.RotatePin("alice", "PIN1");

        var pin = Assert.Single(store.GetAllPins());
        Assert.Equal("alice", pin.Username);
        Assert.Equal("PIN1", pin.Thumbprint);
        Assert.Equal(PinCheckResult.Valid, store.CheckPin("alice", "PIN1"));
    }

    [Fact]
    public void SetPin_PersistsPinsWithoutLeavingTempFile()
    {
        using var tempDir = new TempDir();
        var store = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, tempDir.Path);

        store.SetPin("alice", "PIN1");

        var reloaded = new CertificatePinStore(NullLogger<CertificatePinStore>.Instance, tempDir.Path);
        var pin = Assert.Single(reloaded.GetAllPins());
        Assert.Equal("alice", pin.Username);
        Assert.Equal("PIN1", pin.Thumbprint);
        Assert.False(System.IO.File.Exists(System.IO.Path.Combine(tempDir.Path, "cert_pins.json.tmp")));
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"slskdn-pin-tests-{System.Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
        }
    }
}
