namespace slskd.Tests.Unit.API.Native;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Native;
using slskd.LibraryHealth;
using Xunit;

public class LibraryHealthControllerTests
{
    [Fact]
    public async Task CreateRemediationJob_WithOnlyWhitespaceIssueIds_ReturnsBadRequest()
    {
        var healthService = new Mock<ILibraryHealthService>();
        var controller = CreateController(healthService);

        var result = await controller.CreateRemediationJob(
            new LibraryRemediationRequest(new List<string> { "", "   " }),
            default);

        Assert.IsType<BadRequestObjectResult>(result);
        healthService.Verify(
            service => service.CreateRemediationJobAsync(It.IsAny<List<string>>(), default),
            Times.Never);
    }

    [Fact]
    public async Task CreateRemediationJob_TrimsIssueIdsBeforePassingToService()
    {
        var healthService = new Mock<ILibraryHealthService>();
        healthService
            .Setup(service => service.CreateRemediationJobAsync(It.IsAny<List<string>>(), default))
            .ReturnsAsync("job-123");

        var controller = CreateController(healthService);

        var result = await controller.CreateRemediationJob(
            new LibraryRemediationRequest(new List<string> { " issue-1 ", "issue-2" }),
            default);

        Assert.IsType<OkObjectResult>(result);
        healthService.Verify(
            service => service.CreateRemediationJobAsync(
                It.Is<List<string>>(ids =>
                    ids.Count == 2 &&
                    ids[0] == "issue-1" &&
                    ids[1] == "issue-2"),
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetHealth_WithWhitespacePath_UsesAllPathAndRejectsNonPositiveLimit()
    {
        var healthService = new Mock<ILibraryHealthService>();
        var controller = CreateController(healthService);

        var badRequest = await controller.GetHealth("   ", 0, default);
        Assert.IsType<BadRequestObjectResult>(badRequest);

        healthService
            .Setup(service => service.GetSummaryAsync(string.Empty, default))
            .ReturnsAsync(new LibraryHealthSummary());
        healthService
            .Setup(service => service.GetIssuesAsync(
                It.Is<LibraryHealthIssueFilter>(filter => filter.LibraryPath == string.Empty && filter.Limit == 5),
                default))
            .ReturnsAsync(new List<LibraryHealthIssue>());

        var ok = await controller.GetHealth("   ", 5, default);

        Assert.IsType<OkObjectResult>(ok);
    }

    private static LibraryHealthController CreateController(Mock<ILibraryHealthService> healthService)
    {
        return new LibraryHealthController(
            healthService.Object,
            Mock.Of<IOptionsMonitor<slskd.Options>>(),
            NullLogger<LibraryHealthController>.Instance);
    }
}
