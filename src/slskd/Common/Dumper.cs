// <copyright file="Dumper.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

﻿// <copyright file="Dumper.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.NETCore.Client;

    /// <summary>
    ///     Creates a full memory dump of the current process using Microsoft.Diagnostics.NETCore.Client.
    ///     No network download or shell execution. PR-06.
    /// </summary>
    public class Dumper
    {
        private const long MinFreeBytes = 1024L * 1024 * 1024; // 1 GB

        /// <summary>
        ///     Tries to create a full dump to a temp file. Does not throw for policy or runtime failures.
        /// </summary>
        /// <returns>(ok, error, path): ok true and path set on success; ok false and error set on failure.</returns>
        public async Task<(bool Ok, string? Error, string? Path)> TryCreateDumpAsync(CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(Path.GetTempPath(), $"slskd_{Path.GetRandomFileName()}.dmp");

            if (!HasEnoughFreeSpace(path, MinFreeBytes, out var spaceErr))
            {
                return (false, spaceErr, null);
            }

            try
            {
                var client = new DiagnosticsClient(Environment.ProcessId);
                await client.WriteDumpAsync(DumpType.Full, path, logDumpGeneration: false, cancellationToken).ConfigureAwait(false);
                return (true, null, path);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        private static bool HasEnoughFreeSpace(string filePath, long requiredBytes, out string? error)
        {
            error = null;
            try
            {
                var root = Path.GetPathRoot(filePath);
                if (string.IsNullOrEmpty(root))
                {
                    root = Path.DirectorySeparatorChar.ToString();
                }

                var drive = new DriveInfo(root);
                if (drive.AvailableFreeSpace < requiredBytes)
                {
                    error = "Insufficient free disk space. At least 1 GB required.";
                    return false;
                }

                return true;
            }
            catch
            {
                return true; // best-effort; allow dump if we can't check
            }
        }
    }
}
