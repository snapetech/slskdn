// <copyright file="ExceptionHandlerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-05: Exception handler. In non-Development: no leak of exception message; response has traceId and ProblemDetails.
/// </summary>
public class ExceptionHandlerTests
{
    [Fact]
    public async Task Exception_handler_returns_500_ProblemDetails_with_traceId_and_no_leak_in_non_Development()
    {
        using var factory = new ExceptionHandlerTestHostFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Internal Server Error", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("detail").GetString());

        Assert.True(root.TryGetProperty("traceId", out var traceIdElm), "Response must contain traceId");
        var traceId = traceIdElm.GetString();
        Assert.False(string.IsNullOrEmpty(traceId), "traceId must be non-empty");

        Assert.DoesNotContain("secret-internal-message", json);
    }

    [Fact]
    public async Task Exception_handler_response_shape_has_extensions_traceId()
    {
        using var factory = new ExceptionHandlerTestHostFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/throw");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // traceId can be at root (System.Text.Json flattens ProblemDetails.Extensions) or under "extensions"
        var hasTraceId = root.TryGetProperty("traceId", out _) ||
            (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("traceId", out _));
        Assert.True(hasTraceId, "Response must have traceId (root or extensions.traceId)");
    }

    /// <summary>
    /// §11: FeatureNotImplementedException is mapped to 501 Not Implemented.
    /// </summary>
    [Fact]
    public async Task Exception_handler_FeatureNotImplementedException_returns_501()
    {
        using var factory = new ExceptionHandlerTestHostFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/throw/notimplemented");

        Assert.Equal(501, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(501, root.GetProperty("status").GetInt32());
        Assert.Equal("Not Implemented", root.GetProperty("title").GetString());
        Assert.Contains("not implemented", root.GetProperty("detail").GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
