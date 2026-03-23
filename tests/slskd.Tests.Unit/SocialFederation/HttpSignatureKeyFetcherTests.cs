// <copyright file="HttpSignatureKeyFetcherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.SocialFederation;
using Xunit;

public class HttpSignatureKeyFetcherTests
{
    [Fact]
    public async Task FetchPublicKeyPkixAsync_TrimsKeyId_AndAcceptsTopLevelKeyDocument()
    {
        const string keyId = "https://93.184.216.34/users/alice#main-key";
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"https://93.184.216.34/users/alice#main-key\",\"publicKeyPem\":\"-----BEGIN PUBLIC KEY-----\\nAQID\\n-----END PUBLIC KEY-----\"}", Encoding.UTF8, "application/activity+json"),
                RequestMessage = request,
            };

            return Task.FromResult(response);
        });

        var fetcher = new HttpSignatureKeyFetcher(new HttpClient(handler), NullLogger<HttpSignatureKeyFetcher>.Instance);

        var result = await fetcher.FetchPublicKeyPkixAsync($"  {keyId}  ");

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task FetchPublicKeyPkixAsync_AcceptsMatchingKeyFromActorPublicKeyArray()
    {
        const string keyId = "https://93.184.216.34/users/alice#main-key";
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            const string json = "{\"id\":\"https://93.184.216.34/users/alice\",\"publicKey\":[{\"id\":\"https://93.184.216.34/users/alice#other\",\"publicKeyPem\":\"-----BEGIN PUBLIC KEY-----\\nBAUG\\n-----END PUBLIC KEY-----\"},{\"id\":\"https://93.184.216.34/users/alice#main-key\",\"publicKeyPem\":\"-----BEGIN PUBLIC KEY-----\\nAQID\\n-----END PUBLIC KEY-----\"}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/activity+json"),
                RequestMessage = request,
            };

            return Task.FromResult(response);
        });

        var fetcher = new HttpSignatureKeyFetcher(new HttpClient(handler), NullLogger<HttpSignatureKeyFetcher>.Instance);

        var result = await fetcher.FetchPublicKeyPkixAsync(keyId);

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task FetchPublicKeyPkixAsync_RejectsForbiddenFinalRedirectHost()
    {
        const string keyId = "https://93.184.216.34/users/alice#main-key";
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            var redirectedRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost/users/alice#main-key");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"https://localhost/users/alice#main-key\",\"publicKeyPem\":\"-----BEGIN PUBLIC KEY-----\\nAQID\\n-----END PUBLIC KEY-----\"}", Encoding.UTF8, "application/activity+json"),
                RequestMessage = redirectedRequest,
            };

            return Task.FromResult(response);
        });

        var fetcher = new HttpSignatureKeyFetcher(new HttpClient(handler), NullLogger<HttpSignatureKeyFetcher>.Instance);

        var result = await fetcher.FetchPublicKeyPkixAsync(keyId);

        Assert.Null(result);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
