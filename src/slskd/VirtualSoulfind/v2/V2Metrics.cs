// <copyright file="V2Metrics.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// Metrics for v2
namespace slskd.VirtualSoulfind.v2
{
    public static class V2Metrics
    {
        public const string PlansCreated = "v2_plans_created_total";
        public const string PlansExecuted = "v2_plans_executed_total";
        public const string BackendQueries = "v2_backend_queries_total";
        public const string MatchAttempts = "v2_match_attempts_total";
        public const string VerificationSuccess = "v2_verification_success_total";
        public const string VerificationFailure = "v2_verification_failure_total";
        public const string McpChecks = "v2_mcp_checks_total";
    }
}
