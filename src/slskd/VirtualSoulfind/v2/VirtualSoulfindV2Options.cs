// <copyright file="VirtualSoulfindV2Options.cs" company="slskdn Team">
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
    public sealed class VirtualSoulfindV2Options
    {
        public bool Enabled { get; init; } = false;
        public string DefaultPlanningMode { get; init; } = "SoulseekFriendly";
        public int MaxConcurrentPlans { get; init; } = 10;
        public int PlanTimeoutSeconds { get; init; } = 300;
        public bool EnableQualityScoring { get; init; } = true;
        public bool EnableMatchVerification { get; init; } = true;
    }
}
