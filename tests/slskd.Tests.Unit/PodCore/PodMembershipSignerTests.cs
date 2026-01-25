namespace slskd.Tests.Unit.PodCore;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Overlay;
using slskd.PodCore;
using Xunit;

/// <summary>
/// Unit tests for PodMembershipSigner.
/// </summary>
public class PodMembershipSignerTests
{
    private readonly Mock<ILogger<PodMembershipSigner>> mockLogger;
    private readonly Mock<IKeyStore> mockKeyStore;
    private readonly PodMembershipSigner signer;

    public PodMembershipSignerTests()
    {
        mockLogger = new Mock<ILogger<PodMembershipSigner>>();
        mockKeyStore = new Mock<IKeyStore>();
        
        // Generate a test keypair
        var keyPair = Ed25519KeyPair.Generate();
        mockKeyStore.Setup(k => k.Current).Returns(keyPair);
        
        signer = new PodMembershipSigner(mockLogger.Object, mockKeyStore.Object);
    }

    [Fact]
    public async Task SignMembershipAsync_ShouldCreateValidRecord()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var action = "join";

        // Act
        var record = await signer.SignMembershipAsync(podId, peerId, role, action);

        // Assert
        Assert.NotNull(record);
        Assert.Equal(podId, record.PodId);
        Assert.Equal(peerId, record.PeerId);
        Assert.Equal(role, record.Role);
        Assert.Equal(action, record.Action);
        Assert.True(record.TimestampUnixMs > 0);
        Assert.False(string.IsNullOrWhiteSpace(record.PublicKey));
        Assert.False(string.IsNullOrWhiteSpace(record.Signature));
    }

    [Fact]
    public async Task SignMembershipAsync_ShouldUseProvidedPrivateKey()
    {
        // Arrange
        var customKeyPair = Ed25519KeyPair.Generate();
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var action = "join";

        // Act
        var record = await signer.SignMembershipAsync(
            podId, peerId, role, action, 
            signerPrivateKey: customKeyPair.PrivateKey);

        // Assert
        Assert.NotNull(record);
        var recordPublicKey = Convert.FromBase64String(record.PublicKey);
        Assert.True(customKeyPair.PublicKey.SequenceEqual(recordPublicKey));
    }

    [Fact]
    public async Task VerifyMembershipAsync_ShouldVerifyValidSignature()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var action = "join";

        var record = await signer.SignMembershipAsync(podId, peerId, role, action);
        var publicKey = mockKeyStore.Object.Current.PublicKey;

        // Act
        var isValid = await signer.VerifyMembershipAsync(record, publicKey);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task VerifyMembershipAsync_ShouldRejectInvalidSignature()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var action = "join";

        var record = await signer.SignMembershipAsync(podId, peerId, role, action);
        
        // Tamper with the signature
        record.Signature = Convert.ToBase64String(new byte[64]); // Invalid signature
        
        var publicKey = mockKeyStore.Object.Current.PublicKey;

        // Act
        var isValid = await signer.VerifyMembershipAsync(record, publicKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyMembershipAsync_ShouldRejectWrongPublicKey()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var action = "join";

        var record = await signer.SignMembershipAsync(podId, peerId, role, action);
        
        // Use a different public key
        var wrongKeyPair = Ed25519KeyPair.Generate();
        var wrongPublicKey = wrongKeyPair.PublicKey;

        // Act
        var isValid = await signer.VerifyMembershipAsync(record, wrongPublicKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyMembershipAsync_ShouldRejectRecordWithoutSignature()
    {
        // Arrange
        var record = new SignedMembershipRecord
        {
            PodId = "pod:test-123",
            PeerId = "peer-456",
            Role = "member",
            Action = "join",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PublicKey = Convert.ToBase64String(mockKeyStore.Object.Current.PublicKey),
            Signature = string.Empty // Missing signature
        };
        
        var publicKey = mockKeyStore.Object.Current.PublicKey;

        // Act
        var isValid = await signer.VerifyMembershipAsync(record, publicKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GenerateKeyPair_ShouldCreateValidKeyPair()
    {
        // Act
        var keyPair = signer.GenerateKeyPair();

        // Assert
        Assert.NotNull(keyPair);
        Assert.NotNull(keyPair.PublicKey);
        Assert.NotNull(keyPair.PrivateKey);
        Assert.Equal(32, keyPair.PublicKey.Length); // Ed25519 public keys are 32 bytes
        Assert.Equal(32, keyPair.PrivateKey.Length); // Ed25519 private keys are 32 bytes
        Assert.True(keyPair.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SignMembershipAsync_ShouldCreateDifferentSignaturesForDifferentActions()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";

        // Act
        var joinRecord = await signer.SignMembershipAsync(podId, peerId, role, "join");
        var leaveRecord = await signer.SignMembershipAsync(podId, peerId, role, "leave");

        // Assert — signatures differ because action is in the signable payload
        Assert.NotEqual(joinRecord.Signature, leaveRecord.Signature);
        // TimestampUnixMs may match if both calls occur in the same millisecond
    }

    [Fact]
    public async Task VerifyMembershipAsync_ShouldVerifyAllActionTypes()
    {
        // Arrange
        var podId = "pod:test-123";
        var peerId = "peer-456";
        var role = "member";
        var publicKey = mockKeyStore.Object.Current.PublicKey;

        // Act & Assert
        var joinRecord = await signer.SignMembershipAsync(podId, peerId, role, "join");
        Assert.True(await signer.VerifyMembershipAsync(joinRecord, publicKey));

        var leaveRecord = await signer.SignMembershipAsync(podId, peerId, role, "leave");
        Assert.True(await signer.VerifyMembershipAsync(leaveRecord, publicKey));

        var banRecord = await signer.SignMembershipAsync(podId, peerId, role, "ban");
        Assert.True(await signer.VerifyMembershipAsync(banRecord, publicKey));
    }
}

