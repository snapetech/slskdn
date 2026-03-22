namespace slskd.Tests.Unit.Users
{
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using slskd.Common.Security;
    using slskd.Users;
    using slskd.Users.API;
    using Soulseek;
    using Xunit;

    public class UsersControllerTests
    {
        [Fact]
        public void Group_Returns_User_Group()
        {
            // Arrange
            var userServiceMock = new Mock<IUserService>();
            userServiceMock.Setup(x => x.GetGroup("testuser")).Returns("privileged");

            var soulseekClientMock = new Mock<ISoulseekClient>();
            var browseTrackerMock = new Mock<IBrowseTracker>();
            var safetyLimiterMock = new Mock<ISoulseekSafetyLimiter>();
            var optionsSnapshotMock = new Mock<Microsoft.Extensions.Options.IOptionsSnapshot<slskd.Options>>();

            var controller = new UsersController(
                soulseekClient: soulseekClientMock.Object,
                browseTracker: browseTrackerMock.Object,
                userService: userServiceMock.Object,
                safetyLimiter: safetyLimiterMock.Object,
                optionsSnapshot: optionsSnapshotMock.Object);

            // Act
            var result = controller.Group("testuser");

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal("privileged", okResult.Value);
        }

        [Fact]
        public void Group_Trims_User_Group_Username()
        {
            var userServiceMock = new Mock<IUserService>();
            userServiceMock.Setup(x => x.GetGroup("testuser")).Returns("privileged");

            var controller = new UsersController(
                soulseekClient: Mock.Of<ISoulseekClient>(),
                browseTracker: Mock.Of<IBrowseTracker>(),
                userService: userServiceMock.Object,
                safetyLimiter: Mock.Of<ISoulseekSafetyLimiter>(),
                optionsSnapshot: Mock.Of<Microsoft.Extensions.Options.IOptionsSnapshot<slskd.Options>>());

            var result = controller.Group(" testuser ");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("privileged", okResult.Value);
        }

        [Fact]
        public void Group_With_Blank_Username_Returns_BadRequest()
        {
            var controller = new UsersController(
                soulseekClient: Mock.Of<ISoulseekClient>(),
                browseTracker: Mock.Of<IBrowseTracker>(),
                userService: Mock.Of<IUserService>(),
                safetyLimiter: Mock.Of<ISoulseekSafetyLimiter>(),
                optionsSnapshot: Mock.Of<Microsoft.Extensions.Options.IOptionsSnapshot<slskd.Options>>());

            var result = controller.Group("   ");

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
