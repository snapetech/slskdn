// <copyright file="IObfs4VersionChecker.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Runs the obfs4proxy --version check. Abstraction so tests can inject a stub that returns non‑zero (version failure).
/// </summary>
public interface IObfs4VersionChecker
{
    /// <summary>
    /// Runs the executable with --version and returns its exit code.
    /// </summary>
    /// <param name="executablePath">Path to obfs4proxy (or in tests, any executable).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Process exit code; 0 means the version check succeeded.</returns>
    Task<int> RunVersionCheckAsync(string executablePath, CancellationToken cancellationToken = default);
}
