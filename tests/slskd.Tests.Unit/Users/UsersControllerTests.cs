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
    }
}
