namespace slskd.Tests.Unit.Transfers
{
    using System;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd;
    using slskd.Transfers;
    using AppOptions = slskd.Options;
    using Xunit;

    public class ScheduledRateLimitServiceTests
    {
        [Fact]
        public void GetEffectiveUploadSpeedLimit_Returns_RegularLimit_When_Disabled()
        {
            // Arrange
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                        SpeedLimit = 500,
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = false,
                            NightUploadSpeedLimit = 100
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var service = new ScheduledRateLimitService(optionsMonitorMock.Object);

            // Act
            var result = service.GetEffectiveUploadSpeedLimit();

            // Assert
            Assert.Equal(500, result);
        }

        [Fact]
        public void GetEffectiveDownloadSpeedLimit_Returns_RegularLimit_When_Disabled()
        {
            // Arrange
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Download = new slskd.Options.GlobalOptions.GlobalDownloadOptions()
                    {
                        SpeedLimit = 600,
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = false,
                            NightDownloadSpeedLimit = 200
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var service = new ScheduledRateLimitService(optionsMonitorMock.Object);

            // Act
            var result = service.GetEffectiveDownloadSpeedLimit();

            // Assert
            Assert.Equal(600, result);
        }

        [Fact]
        public void GetEffectiveUploadSpeedLimit_Returns_NightLimit_When_Enabled_And_NightTime()
        {
            // Arrange
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                        SpeedLimit = 1000, // Day limit
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6,
                            NightUploadSpeedLimit = 200 // Night limit
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var nightTime = new DateTime(2025, 12, 13, 2, 0, 0); // 2:00 AM
            var service = new ScheduledRateLimitService(optionsMonitorMock.Object, () => nightTime);

            // Act
            var result = service.GetEffectiveUploadSpeedLimit();

            // Assert
            Assert.Equal(200, result); // Should return night limit
        }

        [Fact]
        public void GetEffectiveDownloadSpeedLimit_Returns_NightLimit_When_Enabled_And_NightTime()
        {
            // Arrange - IsNightTime() uses Global.Upload.ScheduledLimits, so both must be set
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                        ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6
                        }
                    },
                    Download = new slskd.Options.GlobalOptions.GlobalDownloadOptions()
                    {
                        SpeedLimit = 1000, // Day limit
                        ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6,
                            NightDownloadSpeedLimit = 300 // Night limit
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var nightTime = new DateTime(2025, 12, 13, 23, 30, 0); // 11:30 PM
            var service = new ScheduledRateLimitService(optionsMonitorMock.Object, () => nightTime);

            // Act
            var result = service.GetEffectiveDownloadSpeedLimit();

            // Assert
            Assert.Equal(300, result); // Should return night limit
        }

        [Fact]
        public void GetEffectiveUploadSpeedLimit_Returns_DayLimit_When_Enabled_And_DayTime()
        {
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                        SpeedLimit = 1000, // Day limit
                        ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6,
                            NightUploadSpeedLimit = 200 // Night limit
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var dayTime = new DateTime(2025, 12, 13, 14, 0, 0); // 2:00 PM
            var service = new ScheduledRateLimitService(optionsMonitorMock.Object, () => dayTime);

            var result = service.GetEffectiveUploadSpeedLimit();

            Assert.Equal(1000, result); // Should return day limit
        }

        [Fact]
        public void IsNightTime_Returns_True_During_Night_Hours()
        {
            // Arrange
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var nightTimes = new[]
            {
                new DateTime(2025, 12, 13, 22, 0, 0), // 10:00 PM (start)
                new DateTime(2025, 12, 13, 23, 59, 59), // 11:59 PM
                new DateTime(2025, 12, 13, 0, 0, 0), // 12:00 AM
                new DateTime(2025, 12, 13, 5, 59, 59), // 5:59 AM
            };

            foreach (var nightTime in nightTimes)
            {
                var service = new ScheduledRateLimitService(optionsMonitorMock.Object, () => nightTime);
                Assert.True(service.IsNightTime(), $"Should be night time at {nightTime}");
            }
        }

        [Fact]
        public void IsNightTime_Returns_False_During_Day_Hours()
        {
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                        ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22,
                            NightEndHour = 6
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var dayTimes = new[]
            {
                new DateTime(2025, 12, 13, 6, 0, 0), // 6:00 AM (end)
                new DateTime(2025, 12, 13, 12, 0, 0), // 12:00 PM
                new DateTime(2025, 12, 13, 18, 0, 0), // 6:00 PM
                new DateTime(2025, 12, 13, 21, 59, 59), // 9:59 PM
            };

            foreach (var dayTime in dayTimes)
            {
                var service = new ScheduledRateLimitService(optionsMonitorMock.Object, () => dayTime);
                Assert.False(service.IsNightTime(), $"Should be day time at {dayTime}");
            }
        }

        [Fact]
        public void IsNightTime_Handles_Midnight_Wrapping()
        {
            // Arrange - Night period wraps around midnight (22:00 to 06:00)
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = true,
                            NightStartHour = 22, // 10 PM
                            NightEndHour = 6     // 6 AM
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var svc23 = new ScheduledRateLimitService(optionsMonitorMock.Object, () => new DateTime(2025, 12, 13, 23, 0, 0));
            Assert.True(svc23.IsNightTime(), "23:00 should be night time");

            var svc02 = new ScheduledRateLimitService(optionsMonitorMock.Object, () => new DateTime(2025, 12, 13, 2, 0, 0));
            Assert.True(svc02.IsNightTime(), "02:00 should be night time");

            var svc08 = new ScheduledRateLimitService(optionsMonitorMock.Object, () => new DateTime(2025, 12, 13, 8, 0, 0));
            Assert.False(svc08.IsNightTime(), "08:00 should be day time");
        }

        [Fact]
        public void IsNightTime_Returns_False_When_Disabled()
        {
            // Arrange
            var options = new AppOptions()
            {
                Global = new slskd.Options.GlobalOptions()
                {
                    Upload = new slskd.Options.GlobalOptions.GlobalUploadOptions()
                    {
                            ScheduledLimits = new slskd.Options.ScheduledSpeedLimitOptions()
                        {
                            Enabled = false,
                            NightStartHour = 22,
                            NightEndHour = 6
                        }
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<AppOptions>>();
            optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);

            var service = new ScheduledRateLimitService(optionsMonitorMock.Object);

            // Act - any time should return false when disabled
            var result = service.IsNightTime();

            // Assert
            Assert.False(result);
        }

    }
}
