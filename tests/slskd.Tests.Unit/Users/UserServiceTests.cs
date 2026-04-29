// <copyright file="UserServiceTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

// <copyright file="UserServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Users
{
    using System.Collections.Generic;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Users;
    using Soulseek;
    using Xunit;

    public class UserServiceTests
    {
        [Fact]
        public void Dispose_UnsubscribesOptionsMonitor_AndSoulseekEvents()
        {
            var (service, mocks) = GetFixture();

            Assert.Equal(1, mocks.OptionsMonitor.ListenerCount);

            service.Dispose();

            Assert.Equal(0, mocks.OptionsMonitor.ListenerCount);
            mocks.SoulseekClient.VerifyRemove(x => x.UserStatisticsChanged -= It.IsAny<EventHandler<UserStatistics>>(), Times.Once);
            mocks.SoulseekClient.VerifyRemove(x => x.UserStatusChanged -= It.IsAny<EventHandler<UserStatus>>(), Times.Once);
            mocks.SoulseekClient.VerifyRemove(x => x.LoggedIn -= It.IsAny<EventHandler>(), Times.Once);
            mocks.SoulseekClient.VerifyRemove(x => x.Connected -= It.IsAny<EventHandler>(), Times.Once);
            mocks.SoulseekClient.VerifyRemove(x => x.PrivilegedUserListReceived -= It.IsAny<EventHandler<IReadOnlyCollection<string>>>(), Times.Once);
        }

        public class GetGroup
        {
            [Theory, AutoData]
            public void Returns_Default_For_All_Users_If_No_User_Defined_Groups_Configured(string username)
            {
                var (service, _) = GetFixture();

                Assert.Equal(Application.DefaultGroup, service.GetGroup(username));
            }

            [Fact]
            public void Returns_Default_If_Username_Is_Null()
            {
                var (service, _) = GetFixture();

                Assert.Equal(Application.DefaultGroup, service.GetGroup(null));
            }

            [Theory, AutoData]
            public void Returns_User_Defined_Group(string group, string username)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            group,
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { username },
                            }
                        },
                    }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group, service.GetGroup(username));
            }

            [Theory, AutoData]
            public void Returns_Non_Empty_Built_In_Group_After_User_Is_Removed_From_User_Defined_Group(string group, string username)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Members = new[] { username },
                                }
                            },
                        }
                    }
                };

                var (service, mocks) = GetFixture(options);

                Assert.Equal(group, service.GetGroup(username));

                mocks.OptionsMonitor.RaiseOnChange(new Options());

                var resolvedGroup = service.GetGroup(username);

                Assert.Contains(resolvedGroup, new[] { Application.DefaultGroup, Application.LeecherGroup });
            }
        }

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Groups_When_Options_Change(string group, string user)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        { group, new Options.GroupsOptions.UserDefinedOptions()
                        {
                            Members = new[] { user },
                        } },
                    }
                    }
                };

                // do not pass options; only default groups
                var (service, mocks) = GetFixture();

                // ensure defaults
                Assert.Equal(Application.DefaultGroup, service.GetGroup(user));

                // reconfigure with options
                mocks.OptionsMonitor.RaiseOnChange(options);

                Assert.Equal(group, service.GetGroup(user));
            }

            [Theory, AutoData]
            public void Gives_Lowest_Priority_Group_To_Users_Appearing_In_Multiple_Groups(string group0, string group100, string user)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group100,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions() { Priority = 100 },
                                    Members = new[] { user },
                                }
                            },
                            {
                                group0,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions() { Priority = 0 },
                                    Members = new[] { user },
                                }
                            },
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group0, service.GetGroup(user));
            }
        }

        private static (UserService service, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var service = new UserService(
                mocks.SoulseekClient.Object,
                mocks.OptionsMonitor);

            return (service, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options = null)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options ?? new Options());
            }

            public Mock<ISoulseekClient> SoulseekClient { get; } = new Mock<ISoulseekClient>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; init; }
        }
    }
}
