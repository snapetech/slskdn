// <copyright file="ExternalVisualizerLauncherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Player;

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Player;
using Xunit;

public class ExternalVisualizerLauncherTests
{
    [Fact]
    public void GetStatus_WhenPathIsConfigured_ReportsResolvedExecutable()
    {
        using var executable = TempExecutable.Create();
        var launcher = CreateLauncher(new Options.PlayerOptions.ExternalVisualizerOptions
        {
            Enabled = true,
            Name = "MilkDrop3",
            Path = executable.Path,
        });

        var status = launcher.GetStatus();

        Assert.True(status.Enabled);
        Assert.True(status.Configured);
        Assert.True(status.Available);
        Assert.Equal("MilkDrop3", status.Name);
        Assert.Equal(Path.GetFullPath(executable.Path), status.ResolvedPath);
    }

    [Fact]
    public void Launch_WhenDisabled_DoesNotStartProcess()
    {
        using var executable = TempExecutable.Create();
        var starter = new FakeProcessStarter();
        var launcher = CreateLauncher(new Options.PlayerOptions.ExternalVisualizerOptions
        {
            Enabled = false,
            Path = executable.Path,
        }, starter);

        var result = launcher.Launch();

        Assert.False(result.Started);
        Assert.Contains("disabled", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(starter.LastStartInfo);
    }

    [Fact]
    public void Launch_WhenConfigured_StartsConfiguredExecutableOnly()
    {
        using var executable = TempExecutable.Create();
        var workingDirectory = Path.GetDirectoryName(executable.Path)!;
        var starter = new FakeProcessStarter();
        var launcher = CreateLauncher(new Options.PlayerOptions.ExternalVisualizerOptions
        {
            Arguments = new[] { "--preset-dir", "C:\\MilkDrop\\presets" },
            Enabled = true,
            Name = "MilkDrop3",
            Path = executable.Path,
            WorkingDirectory = workingDirectory,
        }, starter);

        var result = launcher.Launch();

        Assert.True(result.Started);
        Assert.Equal(1234, result.ProcessId);
        Assert.NotNull(starter.LastStartInfo);
        Assert.Equal(Path.GetFullPath(executable.Path), starter.LastStartInfo!.FileName);
        Assert.Equal(Path.GetFullPath(workingDirectory), starter.LastStartInfo.WorkingDirectory);
        Assert.Equal(new[] { "--preset-dir", "C:\\MilkDrop\\presets" }, starter.LastStartInfo.ArgumentList);
        Assert.False(starter.LastStartInfo.UseShellExecute);
    }

    [Fact]
    public void Launch_WhenPathIsMissing_DoesNotStartProcess()
    {
        var starter = new FakeProcessStarter();
        var launcher = CreateLauncher(new Options.PlayerOptions.ExternalVisualizerOptions
        {
            Enabled = true,
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe"),
        }, starter);

        var result = launcher.Launch();

        Assert.False(result.Started);
        Assert.Contains("does not exist", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(starter.LastStartInfo);
    }

    private static ExternalVisualizerLauncher CreateLauncher(
        Options.PlayerOptions.ExternalVisualizerOptions externalVisualizerOptions,
        FakeProcessStarter? starter = null)
    {
        return new ExternalVisualizerLauncher(
            new TestOptionsMonitor<Options>(new Options
            {
                Player = new Options.PlayerOptions
                {
                    ExternalVisualizer = externalVisualizerOptions,
                },
            }),
            starter ?? new FakeProcessStarter(),
            NullLogger<ExternalVisualizerLauncher>.Instance);
    }

    private sealed class FakeProcessStarter : IExternalProcessStarter
    {
        public ProcessStartInfo? LastStartInfo { get; private set; }

        public int Start(ProcessStartInfo startInfo)
        {
            LastStartInfo = startInfo;
            return 1234;
        }
    }

    private sealed class TempExecutable : IDisposable
    {
        private TempExecutable(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempExecutable Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"slskdn-external-visualizer-{Guid.NewGuid():N}.exe");
            File.WriteAllText(path, string.Empty);
            return new TempExecutable(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
