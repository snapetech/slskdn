// <copyright file="CsrfPortScopedTokenIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Integration.Security;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Tests.Integration.Harness;
using Xunit;

public class CsrfPortScopedTokenIntegrationTests
{
    [Fact]
    public async Task SharesRescan_WithRealPortScopedToken_AvoidsCsrfFailure()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        await using var runner = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"csrf-port-token-{Guid.NewGuid():N}");

        await runner.StartAsync(disableAuthentication: true);

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(runner.ApiUrl),
        };

        using var bootstrapResponse = await client.GetAsync("/api/v0/session/enabled");
        bootstrapResponse.EnsureSuccessStatusCode();

        var setCookieHeaders = bootstrapResponse.Headers.TryGetValues("Set-Cookie", out var rawSetCookieHeaders)
            ? string.Join(" || ", rawSetCookieHeaders)
            : "<none>";
        var cookies = handler.CookieContainer.GetCookies(new Uri(runner.ApiUrl)).Cast<Cookie>().ToList();
        var cookieSummary = string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));
        Assert.True(
            cookies.Any(cookie => cookie.Name == $"XSRF-COOKIE-{runner.ApiPort}"),
            $"Expected antiforgery cookie XSRF-COOKIE-{runner.ApiPort}. Set-Cookie: {setCookieHeaders}. Cookies: {cookieSummary}");
        Assert.True(
            cookies.Any(cookie => cookie.Name == $"XSRF-TOKEN-{runner.ApiPort}"),
            $"Expected request token cookie XSRF-TOKEN-{runner.ApiPort}. Set-Cookie: {setCookieHeaders}. Cookies: {cookieSummary}");
        var requestToken = cookies.Single(cookie => cookie.Name == $"XSRF-TOKEN-{runner.ApiPort}").Value;

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v0/shares");
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", requestToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode != HttpStatusCode.BadRequest,
            $"Expected CSRF validation to pass, but received {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SafeRequest_WithStaleAntiforgeryCookies_ReissuesFreshPortScopedTokens()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        await using var runner = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"csrf-port-token-stale-{Guid.NewGuid():N}");

        await runner.StartAsync(disableAuthentication: true);

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(runner.ApiUrl),
        };

        var baseUri = new Uri(runner.ApiUrl);
        handler.CookieContainer.Add(baseUri, new Cookie($"XSRF-COOKIE-{runner.ApiPort}", "stale-cookie"));
        handler.CookieContainer.Add(baseUri, new Cookie($"XSRF-TOKEN-{runner.ApiPort}", "stale-request-token"));
        handler.CookieContainer.Add(baseUri, new Cookie("XSRF-TOKEN", "legacy-stale-token"));

        using var bootstrapResponse = await client.GetAsync("/api/v0/session/enabled");
        bootstrapResponse.EnsureSuccessStatusCode();

        var cookies = handler.CookieContainer.GetCookies(baseUri).Cast<Cookie>().ToList();
        var antiforgeryCookie = cookies.Single(cookie => cookie.Name == $"XSRF-COOKIE-{runner.ApiPort}");
        var requestTokenCookie = cookies.Single(cookie => cookie.Name == $"XSRF-TOKEN-{runner.ApiPort}");

        Assert.NotEqual("stale-cookie", antiforgeryCookie.Value);
        Assert.NotEqual("stale-request-token", requestTokenCookie.Value);
        Assert.DoesNotContain(cookies, cookie => cookie.Name == "XSRF-TOKEN");
    }

    [Fact]
    public async Task SharesRescan_WithWrongToken_StillFailsCsrfValidation()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        await using var runner = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"csrf-port-token-negative-{Guid.NewGuid():N}");

        await runner.StartAsync(disableAuthentication: true);

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(runner.ApiUrl),
        };

        using var bootstrapResponse = await client.GetAsync("/api/v0/session/enabled");
        bootstrapResponse.EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v0/shares");
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", "definitely-wrong-token");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CSRF token validation failed", body);
    }
}
