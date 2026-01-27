// <copyright file="JobsControllerPaginationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.API.Native;
using slskd.Jobs;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Unit tests for Jobs API pagination, sorting, and filtering (T-1410).
/// </summary>
public class JobsControllerPaginationTests
{
    private readonly ITestOutputHelper output;
    private readonly Mock<IDiscographyJobService> discographyService;
    private readonly Mock<ILabelCrateJobService> labelCrateService;
    private readonly Mock<IJobServiceWithList> jobServiceList;
    private readonly Mock<ILogger<JobsController>> logger;
    private readonly JobsController controller;

    public JobsControllerPaginationTests(ITestOutputHelper output)
    {
        this.output = output;
        discographyService = new Mock<IDiscographyJobService>();
        labelCrateService = new Mock<ILabelCrateJobService>();
        jobServiceList = new Mock<IJobServiceWithList>();
        logger = new Mock<ILogger<JobsController>>();

        controller = new JobsController(
            discographyService.Object,
            labelCrateService.Object,
            logger.Object,
            jobServiceList.Object);

        // Set up controller context with authenticated user (required for [Authorize])
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "test"))
            }
        };
    }

    [Fact]
    public async Task GetJobs_Should_Apply_Pagination_With_Limit_And_Offset()
    {
        // Arrange - Create 10 jobs
        var jobs = Enumerable.Range(1, 10).Select(i => new DiscographyJob
        {
            JobId = $"job{i}",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
        }).ToList();

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(jobs);
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act - Request first 3 jobs
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: 3,
            offset: 0,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Use reflection to access anonymous object properties
        var responseType = okResult.Value.GetType();
        var totalProp = responseType.GetProperty("total");
        var limitProp = responseType.GetProperty("limit");
        var offsetProp = responseType.GetProperty("offset");
        var hasMoreProp = responseType.GetProperty("has_more");
        var jobsProp = responseType.GetProperty("jobs");
        
        Assert.NotNull(totalProp);
        Assert.NotNull(limitProp);
        Assert.NotNull(offsetProp);
        Assert.NotNull(hasMoreProp);
        Assert.NotNull(jobsProp);
        
        Assert.Equal(10, (int)totalProp.GetValue(okResult.Value));
        Assert.Equal(3, (int)limitProp.GetValue(okResult.Value));
        Assert.Equal(0, (int)offsetProp.GetValue(okResult.Value));
        Assert.True((bool)hasMoreProp.GetValue(okResult.Value));
        
        var jobsList = jobsProp.GetValue(okResult.Value) as System.Collections.IEnumerable;
        Assert.NotNull(jobsList);
        var jobsCount = jobsList.Cast<object>().Count();
        Assert.Equal(3, jobsCount);
    }

    [Fact]
    public async Task GetJobs_Should_Apply_Offset_Correctly()
    {
        // Arrange
        var jobs = Enumerable.Range(1, 10).Select(i => new DiscographyJob
        {
            JobId = $"job{i}",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
        }).ToList();

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(jobs);
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act - Request jobs starting at offset 5
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: 3,
            offset: 5,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var jobsProp = responseType.GetProperty("jobs");
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        Assert.Equal(3, jobsList.Count());
        // Should get jobs 6, 7, 8 (0-indexed, sorted by created_at desc)
    }

    [Fact]
    public async Task GetJobs_Should_Sort_By_CreatedAt_Descending_By_Default()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var jobs = new List<DiscographyJob>
        {
            new() { JobId = "old", Status = JobStatus.Pending, CreatedAt = now.AddHours(-2) },
            new() { JobId = "new", Status = JobStatus.Pending, CreatedAt = now },
            new() { JobId = "mid", Status = JobStatus.Pending, CreatedAt = now.AddHours(-1) }
        };

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(jobs);
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: null,
            offset: null,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var jobsProp = responseType.GetProperty("jobs");
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        
        // Should be sorted newest first - use reflection to get id property
        var job1Type = jobsList[0].GetType();
        var id1Prop = job1Type.GetProperty("id");
        var job2Type = jobsList[1].GetType();
        var id2Prop = job2Type.GetProperty("id");
        var job3Type = jobsList[2].GetType();
        var id3Prop = job3Type.GetProperty("id");
        
        Assert.Equal("new", id1Prop.GetValue(jobsList[0]) as string);
        Assert.Equal("mid", id2Prop.GetValue(jobsList[1]) as string);
        Assert.Equal("old", id3Prop.GetValue(jobsList[2]) as string);
    }

    [Fact]
    public async Task GetJobs_Should_Sort_By_Status_When_Specified()
    {
        // Arrange
        var jobs = new List<DiscographyJob>
        {
            new() { JobId = "running", Status = JobStatus.Running, CreatedAt = DateTimeOffset.UtcNow },
            new() { JobId = "pending", Status = JobStatus.Pending, CreatedAt = DateTimeOffset.UtcNow },
            new() { JobId = "completed", Status = JobStatus.Completed, CreatedAt = DateTimeOffset.UtcNow }
        };

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(jobs);
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act - Sort by status ascending
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: null,
            offset: null,
            sortBy: "status",
            sortOrder: "asc",
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var jobsProp = responseType.GetProperty("jobs");
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        
        // Status order: completed, pending, running (alphabetical)
        var jobType = jobsList[0].GetType();
        var statusProp = jobType.GetProperty("status");
        var statuses = jobsList.Select(j => statusProp.GetValue(j) as string).ToList();
        Assert.Contains("completed", statuses);
        Assert.Contains("pending", statuses);
        Assert.Contains("running", statuses);
    }

    [Fact]
    public async Task GetJobs_Should_Handle_Empty_Result_Set()
    {
        // Arrange
        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(new List<DiscographyJob>());
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: 10,
            offset: 0,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var totalProp = responseType.GetProperty("total");
        var hasMoreProp = responseType.GetProperty("has_more");
        var jobsProp = responseType.GetProperty("jobs");
        
        Assert.Equal(0, (int)totalProp.GetValue(okResult.Value));
        Assert.False((bool)hasMoreProp.GetValue(okResult.Value));
        
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        Assert.Empty(jobsList);
    }

    [Fact]
    public async Task GetJobs_Should_Include_Progress_And_CreatedAt_In_Response()
    {
        // Arrange
        var job = new DiscographyJob
        {
            JobId = "testjob",
            Status = JobStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            TotalReleases = 10,
            CompletedReleases = 5,
            FailedReleases = 1
        };

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(new List<DiscographyJob> { job });
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: null,
            offset: null,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var jobsProp = responseType.GetProperty("jobs");
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        var firstJob = jobsList.First();
        
        var jobType = firstJob.GetType();
        var createdAtProp = jobType.GetProperty("created_at");
        var progressProp = jobType.GetProperty("progress");
        
        Assert.NotNull(createdAtProp.GetValue(firstJob));
        var progress = progressProp.GetValue(firstJob);
        Assert.NotNull(progress);
        
        var progressType = progress.GetType();
        var totalProp = progressType.GetProperty("releases_total");
        var doneProp = progressType.GetProperty("releases_done");
        var failedProp = progressType.GetProperty("releases_failed");
        
        Assert.Equal(10, (int)totalProp.GetValue(progress));
        Assert.Equal(5, (int)doneProp.GetValue(progress));
        Assert.Equal(1, (int)failedProp.GetValue(progress));
    }

    [Fact]
    public async Task GetJobs_Should_Handle_Large_Offset_Gracefully()
    {
        // Arrange
        var jobs = Enumerable.Range(1, 5).Select(i => new DiscographyJob
        {
            JobId = $"job{i}",
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        jobServiceList.Setup(x => x.GetAllDiscographyJobs()).Returns(jobs);
        jobServiceList.Setup(x => x.GetAllLabelCrateJobs()).Returns(new List<LabelCrateJob>());

        // Act - Request offset beyond available jobs
        var result = await controller.GetJobs(
            type: null,
            status: null,
            limit: 10,
            offset: 100,
            sortBy: null,
            sortOrder: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value.GetType();
        var totalProp = responseType.GetProperty("total");
        var offsetProp = responseType.GetProperty("offset");
        var hasMoreProp = responseType.GetProperty("has_more");
        var jobsProp = responseType.GetProperty("jobs");
        
        Assert.Equal(5, (int)totalProp.GetValue(okResult.Value));
        Assert.Equal(100, (int)offsetProp.GetValue(okResult.Value));
        Assert.False((bool)hasMoreProp.GetValue(okResult.Value));
        
        var jobsList = (jobsProp.GetValue(okResult.Value) as IEnumerable).Cast<object>().ToList();
        Assert.Empty(jobsList);
    }
}
