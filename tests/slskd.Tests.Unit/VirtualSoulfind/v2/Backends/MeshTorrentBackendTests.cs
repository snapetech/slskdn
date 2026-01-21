// MeshDht and Torrent backend tests
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    public class MeshDhtBackendTests
    {
        [Fact]
        public void MeshDhtBackend_HasCorrectType()
        {
            var backend = CreateMeshBackend();
            Assert.Equal(ContentBackendType.MeshDht, backend.Type);
            Assert.Null(backend.SupportedDomain);
        }

        [Fact]
        public async Task FindCandidates_Disabled_ReturnsEmpty()
        {
            var options = new MeshDhtBackendOptions { Enabled = false };
            var backend = CreateMeshBackend(options);
            var itemId = ContentItemId.NewId();
            
            var candidates = await backend.FindCandidatesAsync(itemId);
            
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsNonMesh()
        {
            var backend = CreateMeshBackend();
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Http,
                BackendRef = "https://test.com/file.flac",
                TrustScore = 0.7f,
                ExpectedQuality = 80,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("Not a MeshDht", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_AcceptsValidCandidate()
        {
            var options = new MeshDhtBackendOptions { Enabled = true, MinimumTrustScore = 0.3f };
            var backend = CreateMeshBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh:node:abc123",
                TrustScore = 0.8f,
                ExpectedQuality = 90,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.True(result.IsValid);
        }

        private MeshDhtBackend CreateMeshBackend(MeshDhtBackendOptions options = null)
        {
            options ??= new MeshDhtBackendOptions { Enabled = true };
            var registry = new InMemorySourceRegistry();
            var optionsMonitor = new Mock<IOptionsMonitor<MeshDhtBackendOptions>>();
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);
            return new MeshDhtBackend(registry, optionsMonitor.Object);
        }
    }

    public class TorrentBackendTests
    {
        [Fact]
        public void TorrentBackend_HasCorrectType()
        {
            var backend = CreateTorrentBackend();
            Assert.Equal(ContentBackendType.Torrent, backend.Type);
            Assert.Null(backend.SupportedDomain);
        }

        [Fact]
        public async Task FindCandidates_Disabled_ReturnsEmpty()
        {
            var options = new TorrentBackendOptions { Enabled = false };
            var backend = CreateTorrentBackend(options);
            var itemId = ContentItemId.NewId();
            
            var candidates = await backend.FindCandidatesAsync(itemId);
            
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_AcceptsValidInfohash()
        {
            var options = new TorrentBackendOptions { Enabled = true, MinimumSeeders = 1 };
            var backend = CreateTorrentBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Torrent,
                BackendRef = "1234567890abcdef1234567890abcdef12345678",
                TrustScore = 0.8f,
                ExpectedQuality = 10,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateCandidate_AcceptsMagnetLink()
        {
            var options = new TorrentBackendOptions { Enabled = true, MinimumSeeders = 1 };
            var backend = CreateTorrentBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Torrent,
                BackendRef = "magnet:?xt=urn:btih:1234567890abcdef1234567890abcdef12345678",
                TrustScore = 0.8f,
                ExpectedQuality = 5,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsInvalidInfohash()
        {
            var backend = CreateTorrentBackend();
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Torrent,
                BackendRef = "not-an-infohash",
                TrustScore = 0.8f,
                ExpectedQuality = 10,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("Invalid infohash", result.InvalidityReason);
        }

        private TorrentBackend CreateTorrentBackend(TorrentBackendOptions options = null)
        {
            options ??= new TorrentBackendOptions { Enabled = true };
            var registry = new InMemorySourceRegistry();
            var optionsMonitor = new Mock<IOptionsMonitor<TorrentBackendOptions>>();
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);
            return new TorrentBackend(registry, optionsMonitor.Object);
        }
    }
}
