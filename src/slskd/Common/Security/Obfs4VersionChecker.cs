// <copyright file="Obfs4VersionChecker.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace slskd.Common.Security;

/// <summary>
/// Default implementation that runs the obfs4proxy binary with --version.
/// </summary>
public sealed class Obfs4VersionChecker : IObfs4VersionChecker
{
    /// <inheritdoc />
    public async Task<int> RunVersionCheckAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        process.Start();
        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        return process.ExitCode;
    }
}
