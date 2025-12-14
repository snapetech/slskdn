// <copyright file="AnalyzerMigrationController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio.API
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/audio/analyzers/migrate")]
    public class AnalyzerMigrationController : ControllerBase
    {
        private readonly IAnalyzerMigrationService migrationService;

        public AnalyzerMigrationController(IAnalyzerMigrationService migrationService)
        {
            this.migrationService = migrationService;
        }

        /// <summary>
        ///     Recompute analyzer outputs for variants missing/stale analyzer_version.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> Migrate([FromQuery] string targetVersion = "audioqa-1", CancellationToken ct = default)
        {
            var updated = await migrationService.MigrateAsync(targetVersion, ct).ConfigureAwait(false);
            return Ok(new { updated, targetVersion });
        }
    }
}
