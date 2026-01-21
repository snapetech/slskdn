// <copyright file="ScheduledRateLimitService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Options;
using System;

namespace slskd.Transfers
{
    /// <summary>
    ///     Service for managing scheduled rate limits (day/night speed schedules).
    /// </summary>
    public interface IScheduledRateLimitService
    {
        /// <summary>
        ///     Gets the effective upload speed limit based on current time and schedule.
        /// </summary>
        int GetEffectiveUploadSpeedLimit();

        /// <summary>
        ///     Gets the effective download speed limit based on current time and schedule.
        /// </summary>
        int GetEffectiveDownloadSpeedLimit();

        /// <summary>
        ///     Checks if currently in night period (lower speed limits).
        /// </summary>
        bool IsNightTime();
    }

    /// <summary>
    ///     Service for managing scheduled rate limits (day/night speed schedules).
    /// </summary>
    public class ScheduledRateLimitService : IScheduledRateLimitService
    {
        private readonly IOptionsMonitor<Options> _optionsMonitor;

        public ScheduledRateLimitService(IOptionsMonitor<Options> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Gets the effective upload speed limit based on current time and schedule.
        /// </summary>
        public int GetEffectiveUploadSpeedLimit()
        {
            var options = _optionsMonitor.CurrentValue;
            var scheduledLimits = options.Global.Upload.ScheduledLimits;

            if (!scheduledLimits.Enabled)
            {
                return options.Global.Upload.SpeedLimit;
            }

            return IsNightTime() ? scheduledLimits.NightUploadSpeedLimit : options.Global.Upload.SpeedLimit;
        }

        /// <summary>
        ///     Gets the effective download speed limit based on current time and schedule.
        /// </summary>
        public int GetEffectiveDownloadSpeedLimit()
        {
            var options = _optionsMonitor.CurrentValue;
            var scheduledLimits = options.Global.Download.ScheduledLimits;

            if (!scheduledLimits.Enabled)
            {
                return options.Global.Download.SpeedLimit;
            }

            return IsNightTime() ? scheduledLimits.NightDownloadSpeedLimit : options.Global.Download.SpeedLimit;
        }

        /// <summary>
        ///     Checks if currently in night period (lower speed limits).
        /// </summary>
        public bool IsNightTime()
        {
            var options = _optionsMonitor.CurrentValue;
            var scheduledLimits = options.Global.Upload.ScheduledLimits; // Use upload config as source of truth

            if (!scheduledLimits.Enabled)
            {
                return false;
            }

            var now = DateTime.Now;
            var currentHour = now.Hour;

            var nightStart = scheduledLimits.NightStartHour;
            var nightEnd = scheduledLimits.NightEndHour;

            if (nightStart <= nightEnd)
            {
                // Night period doesn't wrap around midnight (e.g., 22:00 to 06:00)
                return currentHour >= nightStart && currentHour < nightEnd;
            }
            else
            {
                // Night period wraps around midnight (e.g., 22:00 to 06:00 spans midnight)
                return currentHour >= nightStart || currentHour < nightEnd;
            }
        }
    }
}

