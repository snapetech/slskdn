// HTTP Backend tests (SIMPLIFIED FOR SPEED)
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    public class HttpBackendTests
    {
        [Fact]
        public void HttpBackend_HasCorrectType()
        {
            var backend = CreateBackend();
            Assert.Equal(ContentBackendType.Http, backend.Type);
            Assert.Null(backend.SupportedDomain);
        }

        [Fact]
        public async Task FindCandidates_NoAllowlist_ReturnsEmpty()
        {
            var options = new HttpBackendOptions { DomainAllowlist = new List<string>() };
            var backend = CreateBackend(options);
            var itemId = ContentItemId.Parse("music:track:test123");
            
            var candidates = await backend.FindCandidatesAsync(itemId);
            
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsNonHttp()
        {
            var backend = CreateBackend();
            var candidate = new SourceCandidate
            {
                Id = "cand1",
                ItemId = ContentItemId.Parse("music:track:test123"),
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh:node123",
                TrustScore = 0.7f,
                ExpectedQuality = 80,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("Not an HTTP", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsInvalidUrl()
        {
            var backend = CreateBackend();
            var candidate = new SourceCandidate
            {
                Id = "cand2",
                ItemId = ContentItemId.Parse("music:track:test123"),
                Backend = ContentBackendType.Http,
                BackendRef = "not-a-url",
                TrustScore = 0.7f,
                ExpectedQuality = 80,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("Invalid URL", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsNonAllowlistedDomain()
        {
            var options = new HttpBackendOptions
            {
                DomainAllowlist = new List<string> { "allowed.com" }
            };
            var backend = CreateBackend(options);
            var candidate = new SourceCandidate
            {
                Id = "cand3",
                ItemId = ContentItemId.Parse("music:track:test123"),
                Backend = ContentBackendType.Http,
                BackendRef = "https://evil.com/file.flac",
                TrustScore = 0.7f,
                ExpectedQuality = 80,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("not in allowlist", result.InvalidityReason);
        }

        private HttpBackend CreateBackend(HttpBackendOptions options = null)
        {
            options ??= new HttpBackendOptions
            {
                DomainAllowlist = new List<string> { "allowed.com" }
            };

            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var httpClient = new HttpClient();
            mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var optionsMonitor = new Mock<IOptionsMonitor<HttpBackendOptions>>();
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);

            var registry = new InMemorySourceRegistry();

            return new HttpBackend(mockHttpFactory.Object, optionsMonitor.Object, registry);
        }
    }
}
