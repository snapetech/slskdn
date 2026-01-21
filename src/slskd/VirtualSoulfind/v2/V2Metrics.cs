// <copyright file="V2Metrics.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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
