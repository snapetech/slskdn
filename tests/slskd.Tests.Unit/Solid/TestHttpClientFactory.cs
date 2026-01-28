// <copyright file="TestHttpClientFactory.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Solid;

using System.Net.Http;
using Microsoft.Extensions.Http;

internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public TestHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public HttpClient CreateClient(string name)
    {
        return _client;
    }
}
