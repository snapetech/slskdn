// <copyright file="VirtualSoulfindV2Options.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// Configuration for v2
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
