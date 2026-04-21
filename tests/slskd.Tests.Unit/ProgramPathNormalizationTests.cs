// <copyright file="ProgramPathNormalizationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Soulseek;
using Xunit;

[Collection("ProgramAppDirectory")]
public class ProgramPathNormalizationTests
{
    [Fact]
    public void ResolveAppRelativePath_UsesAppDirectory_ForRelativePaths()
    {
        var originalAppDirectory = Program.AppDirectory;

        try
        {
            SetAppDirectory(Path.Combine(Path.GetTempPath(), $"slskdn-appdir-{Guid.NewGuid():N}"));

            var resolved = Program.ResolveAppRelativePath("mesh-overlay.key", "mesh-overlay.key");

            Assert.Equal(Path.Combine(Program.AppDirectory, "mesh-overlay.key"), resolved);
        }
        finally
        {
            SetAppDirectory(originalAppDirectory);
        }
    }

    [Fact]
    public void ResolveAppRelativePath_KeepsAbsolutePaths()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), $"slskdn-absolute-{Guid.NewGuid():N}", "mesh-overlay.key");

        var resolved = Program.ResolveAppRelativePath(absolutePath, "mesh-overlay.key");

        Assert.Equal(absolutePath, resolved);
    }

    [Fact]
    public void ResolveAppRelativePath_UsesFallback_ForBlankValues()
    {
        var originalAppDirectory = Program.AppDirectory;

        try
        {
            SetAppDirectory(Path.Combine(Path.GetTempPath(), $"slskdn-appdir-{Guid.NewGuid():N}"));

            var resolved = Program.ResolveAppRelativePath(string.Empty, "data");

            Assert.Equal(Path.Combine(Program.AppDirectory, "data"), resolved);
        }
        finally
        {
            SetAppDirectory(originalAppDirectory);
        }
    }

    [Fact]
    public void ResolveOptionalAppRelativePath_UsesAppDirectory_ForRelativePaths()
    {
        var originalAppDirectory = Program.AppDirectory;

        try
        {
            SetAppDirectory(Path.Combine(Path.GetTempPath(), $"slskdn-appdir-{Guid.NewGuid():N}"));

            var resolved = Program.ResolveOptionalAppRelativePath("quarantine");

            Assert.Equal(Path.Combine(Program.AppDirectory, "quarantine"), resolved);
        }
        finally
        {
            SetAppDirectory(originalAppDirectory);
        }
    }

    [Fact]
    public void ResolveOptionalAppRelativePath_LeavesBlankValuesBlank()
    {
        var resolved = Program.ResolveOptionalAppRelativePath(string.Empty);

        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void GetWriteBaseDirectory_UsesDefaultAppDirectory_WhenAppDirectoryIsBlank()
    {
        var originalAppDirectory = Program.AppDirectory;

        try
        {
            SetAppDirectory(string.Empty);

            var baseDirectory = Program.GetWriteBaseDirectory();

            Assert.Equal(Program.DefaultAppDirectory, baseDirectory);
        }
        finally
        {
            SetAppDirectory(originalAppDirectory);
        }
    }

    [Fact]
    public void CreateInitialSoulseekClientOptions_UsesConfiguredListenerAndDistributedNetworkSettings()
    {
        var optionsAtStartup = new OptionsAtStartup
        {
            Soulseek = new Options.SoulseekOptions
            {
                ListenIpAddress = "127.0.0.1",
                ListenPort = 50444,
                DistributedNetwork = new Options.SoulseekOptions.DistributedNetworkOptions
                {
                    ChildLimit = 7,
                    Disabled = false,
                    DisableChildren = false,
                },
            },
        };

        var options = Program.CreateInitialSoulseekClientOptions(optionsAtStartup);

        Assert.True(options.EnableListener);
        Assert.Equal("127.0.0.1", options.ListenIPAddress.ToString());
        Assert.Equal(50444, options.ListenPort);
        Assert.True(options.EnableDistributedNetwork);
        Assert.True(options.AcceptDistributedChildren);
        Assert.Equal(7, options.DistributedChildLimit);
    }

    [Fact]
    public void CreateWebHtmlRewriteRules_PrefixesAssetAndManifestPaths_ForUrlBase()
    {
        var rules = Program.CreateWebHtmlRewriteRules("/system");

        var html = """
            <link rel="manifest" href="/manifest.json" />
            <link rel="apple-touch-icon" href="/logo192.png" />
            <script type="module" src="/assets/index.js"></script>
            """;

        foreach (var (pattern, replacement) in rules)
        {
            html = System.Text.RegularExpressions.Regex.Replace(html, pattern, replacement);
        }

        Assert.Contains("href=\"/system/manifest.json\"", html);
        Assert.Contains("href=\"/system/logo192.png\"", html);
        Assert.Contains("src=\"/system/assets/index.js\"", html);
    }

    [Fact]
    public void IsBenignUnobservedTaskException_ReturnsFalse_ForConnectionRefusedSocketFailure()
    {
        var exception = new AggregateException(new SocketException((int)SocketError.ConnectionRefused));

        Assert.False(Program.IsBenignUnobservedTaskException(exception));
    }

    [Fact]
    public void IsBenignUnobservedTaskException_ReturnsFalse_ForUnexpectedFailures()
    {
        var exception = new AggregateException(new InvalidOperationException("boom"));

        Assert.False(Program.IsBenignUnobservedTaskException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForDisposedConnectionFailures()
    {
        var exception = new AggregateException(new ObjectDisposedException("Connection"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForPierceFirewallConnectionFailures()
    {
        var exception = new AggregateException(new IOException(
            "Unknown PierceFirewall attempt with token 46 from x.x.x.x:44490 (id: abcdef)"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForRemoteConnectionClosedFailures()
    {
        var exception = new AggregateException(new IOException("Remote connection closed"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForConnectionRefusedFailures()
    {
        var exception = new AggregateException(new IOException("Connection refused"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForConnectionResetByPeerFailures()
    {
        var exception = new AggregateException(new SocketException((int)SocketError.ConnectionReset));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForRemoteTransferReportedFailedFailures()
    {
        var exception = new AggregateException(new TransferReportedFailedException("Download reported as failed by remote client"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForRemoteTransferRejectedEnqueueFailures()
    {
        var exception = new AggregateException(new TransferRejectedException("Enqueue failed due to internal error"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForTransferCompleteTeardownFailures()
    {
        var exception = new AggregateException(new ConnectionException("Transfer failed: Transfer complete"));

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForSoulseekMessageConnectionClosedTeardown()
    {
        var inner = new InvalidOperationException("The underlying Tcp connection is closed");
        ExceptionDispatchInfo.SetRemoteStackTrace(
            inner,
            "   at Soulseek.Network.MessageConnection.ReadContinuouslyAsync()");
        var exception = new AggregateException(inner);

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForSoulseekTimerResetReadLoopRace()
    {
        var inner = new NullReferenceException("Object reference not set to an instance of an object.");
        ExceptionDispatchInfo.SetRemoteStackTrace(
            inner,
            "   at Soulseek.Extensions.Reset(Timer)\n" +
            "   at Soulseek.Network.Tcp.Connection.ReadInternalAsync(Int64 length, Stream outputStream, Func`3 governor, Action`3 reporter, CancellationToken cancellationToken)\n" +
            "   at Soulseek.Network.MessageConnection.ReadContinuouslyAsync()");
        var exception = new AggregateException(inner);

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForSoulseekTimerResetWriteLoopRace()
    {
        var inner = new NullReferenceException("Object reference not set to an instance of an object.");
        ExceptionDispatchInfo.SetRemoteStackTrace(
            inner,
            "   at Soulseek.Extensions.Reset(Timer)\n" +
            "   at Soulseek.Network.Tcp.Connection.WriteInternalAsync(Int64 length, Stream inputStream, Func`3 governor, Action`3 reporter, CancellationToken cancellationToken)");
        var exception = new AggregateException(inner);

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsExpectedSoulseekNetworkException_ReturnsTrue_ForSoulseekTcpDoubleDisconnectRace()
    {
        var inner = new InvalidOperationException("An attempt was made to transition a task to a final state when it had already completed.");
        ExceptionDispatchInfo.SetRemoteStackTrace(
            inner,
            "   at Soulseek.Network.Tcp.Connection.Disconnect(String message, Exception exception)\n" +
            "   at Soulseek.Network.Tcp.Connection.ReadInternalAsync(Int64 length, Stream outputStream, Func`3 governor, Action`3 reporter, CancellationToken cancellationToken)\n" +
            "   at Soulseek.Network.MessageConnection.ReadContinuouslyAsync()");
        var exception = new AggregateException(inner);

        Assert.True(Program.IsExpectedSoulseekNetworkException(exception));
    }

    [Fact]
    public void IsStaleAntiforgeryTokenException_ReturnsTrue_ForKeyRingMismatch()
    {
        var exception = new AntiforgeryValidationException("The antiforgery token could not be decrypted.", new CryptographicException("The key {abc} was not found in the key ring."));

        Assert.True(Program.IsStaleAntiforgeryTokenException(exception));
    }

    [Fact]
    public void StripKnownAntiforgeryCookiesFromRequest_RemovesXsrfCookies_AndResetsParsedRequestCookies()
    {
        var port = GetProgramValue<OptionsAtStartup>("OptionsAtStartup").Web.Port;
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"session=keep; XSRF-COOKIE-{port}=stale-cookie; theme=dark; XSRF-TOKEN-{port}=stale-request; XSRF-TOKEN=legacy";

        _ = context.Request.Cookies;
        Assert.NotNull(context.Features.Get<IRequestCookiesFeature>());

        var stripped = Program.StripKnownAntiforgeryCookiesFromRequest(context);

        Assert.True(stripped);
        Assert.Equal("session=keep; theme=dark", context.Request.Headers.Cookie.ToString());
        Assert.Equal("keep", context.Request.Cookies["session"]);
        Assert.Equal("dark", context.Request.Cookies["theme"]);
        Assert.False(context.Request.Cookies.ContainsKey($"XSRF-COOKIE-{port}"));
        Assert.False(context.Request.Cookies.ContainsKey($"XSRF-TOKEN-{port}"));
        Assert.False(context.Request.Cookies.ContainsKey("XSRF-TOKEN"));
    }

    [Fact]
    public void TryGetAndStoreAntiforgeryTokens_Retries_AfterClearingDirectCryptographicFailure()
    {
        var port = GetProgramValue<OptionsAtStartup>("OptionsAtStartup").Web.Port;
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Path = "/api/v0/session/enabled";
        context.Request.Headers.Cookie = $"XSRF-COOKIE-{port}=stale-cookie; XSRF-TOKEN-{port}=stale-request";

        var expectedTokens = new AntiforgeryTokenSet("fresh-request", "fresh-cookie", "X-CSRF-TOKEN", $"XSRF-COOKIE-{port}");
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .SetupSequence(mock => mock.GetAndStoreTokens(context))
            .Throws(new CryptographicException("The key {abc} was not found in the key ring."))
            .Returns(expectedTokens);

        var tokens = Program.TryGetAndStoreAntiforgeryTokens(context, antiforgery.Object);

        Assert.Same(expectedTokens, tokens);
        antiforgery.Verify(mock => mock.GetAndStoreTokens(context), Times.Exactly(2));
        Assert.Contains(context.Response.Headers["Set-Cookie"], value => value.Contains($"XSRF-COOKIE-{port}=;", StringComparison.Ordinal));
        Assert.Contains(context.Response.Headers["Set-Cookie"], value => value.Contains($"XSRF-TOKEN-{port}=;", StringComparison.Ordinal));
    }

    [Fact]
    public void TryGetAndStoreAntiforgeryTokens_Retries_AfterClearingStaleCookies()
    {
        var port = GetProgramValue<OptionsAtStartup>("OptionsAtStartup").Web.Port;
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Path = "/api/v0/session/enabled";
        context.Request.Headers.Cookie = $"XSRF-COOKIE-{port}=stale-cookie; XSRF-TOKEN-{port}=stale-request";

        var expectedTokens = new AntiforgeryTokenSet("fresh-request", "fresh-cookie", "X-CSRF-TOKEN", $"XSRF-COOKIE-{port}");
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .SetupSequence(mock => mock.GetAndStoreTokens(context))
            .Throws(new AntiforgeryValidationException("The antiforgery token could not be decrypted.", new CryptographicException("The key {abc} was not found in the key ring.")))
            .Returns(expectedTokens);

        var tokens = Program.TryGetAndStoreAntiforgeryTokens(context, antiforgery.Object);

        Assert.Same(expectedTokens, tokens);
        antiforgery.Verify(mock => mock.GetAndStoreTokens(context), Times.Exactly(2));
        Assert.Contains(context.Response.Headers["Set-Cookie"], value => value.Contains($"XSRF-COOKIE-{port}=;", StringComparison.Ordinal));
        Assert.Contains(context.Response.Headers["Set-Cookie"], value => value.Contains($"XSRF-TOKEN-{port}=;", StringComparison.Ordinal));
    }

    private static T GetProgramValue<T>(string propertyName)
    {
        var property = typeof(Program).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(property);
        return (T)property!.GetValue(null)!;
    }


    private static void SetAppDirectory(string value)
    {
        var property = typeof(Program).GetProperty(nameof(Program.AppDirectory), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(property);
        var setter = property!.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(null, new object[] { value });
    }
}
