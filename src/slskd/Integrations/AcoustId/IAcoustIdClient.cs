// <copyright file="IAcoustIdClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AcoustId
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Integrations.AcoustId.Models;

    /// <summary>
    ///     Client for calling the AcoustID web service.
    /// </summary>
    public interface IAcoustIdClient
    {
        /// <summary>
        ///     Looks up the fingerprint and returns AcoustID records.
        /// </summary>
        Task<AcoustIdResult?> LookupAsync(string fingerprint, int sampleRate, int durationSeconds, CancellationToken cancellationToken = default);
    }
}


