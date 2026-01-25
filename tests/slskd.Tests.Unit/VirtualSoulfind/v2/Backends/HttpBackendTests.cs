// HTTP Backend tests (SIMPLIFIED FOR SPEED)
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
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
            var itemId = ContentItemId.NewId();
            
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);
            
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsNonHttp()
        {
            var backend = CreateBackend();
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh:node123",
                TrustScore = 0.7f,
                ExpectedQuality = 0.8f,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);
            
            Assert.False(result.IsValid);
            Assert.Contains("Not an HTTP", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsInvalidUrl()
        {
            var backend = CreateBackend();
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Http,
                BackendRef = "not-a-url",
                TrustScore = 0.7f,
                ExpectedQuality = 0.8f,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);
            
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
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Http,
                BackendRef = "https://evil.com/file.flac",
                TrustScore = 0.7f,
                ExpectedQuality = 0.8f,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);
            
            Assert.False(result.IsValid);
            Assert.Contains("not in allowlist", result.InvalidityReason);
        }

        private HttpBackend CreateBackend(HttpBackendOptions options = null)
        {
            options ??= new HttpBackendOptions
            {
                DomainAllowlist = new List<string> { "allowed.com" }
            };

            // IHttpClientFactory.CreateClient (and its parameterless extension) must return an HttpClient.
            // Use a test double because Moq cannot setup extension methods.
            var httpClient = new HttpClient();
            var httpFactory = new TestHttpClientFactory(httpClient);

            var optionsMonitor = new Mock<IOptionsMonitor<HttpBackendOptions>>();
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);

            var registry = new InMemorySourceRegistry();

            return new HttpBackend(httpFactory, optionsMonitor.Object, registry);
        }
    }

    internal sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public TestHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
