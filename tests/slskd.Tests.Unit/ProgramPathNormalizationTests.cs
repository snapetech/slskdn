// <copyright file="ProgramPathNormalizationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
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
    public void CreateInitialSoulseekClientOptions_DisablesListenerAndDistributedNetworkUntilStartupReconfiguration()
    {
        var options = Program.CreateInitialSoulseekClientOptions(new OptionsAtStartup());

        Assert.False(options.EnableListener);
        Assert.False(options.EnableDistributedNetwork);
        Assert.False(options.AcceptDistributedChildren);
    }

    [Fact]
    public void IsBenignUnobservedTaskException_ReturnsTrue_ForListenerStartupRace()
    {
        var exception = new AggregateException(new InvalidOperationException("Not listening. You must call the Start() method before calling this method."));

        Assert.True(Program.IsBenignUnobservedTaskException(exception));
    }

    [Fact]
    public void IsBenignUnobservedTaskException_ReturnsTrue_ForConnectionRefusedSocketFailure()
    {
        var exception = new AggregateException(new SocketException((int)SocketError.ConnectionRefused));

        Assert.True(Program.IsBenignUnobservedTaskException(exception));
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

    private static void SetAppDirectory(string value)
    {
        var field = typeof(Program).GetField($"<{nameof(Program.AppDirectory)}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        field!.SetValue(null, value);
    }
}
