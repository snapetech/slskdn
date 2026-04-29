// <copyright file="EventsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Events;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Events;
using slskd.Events.API;
using Xunit;

public class EventsControllerTests
{
    [Fact]
    public void GetEvents_WhenEventServiceThrows_ReturnsSanitized500()
    {
        var eventService = new Mock<EventService>(Mock.Of<IDbContextFactory<EventsDbContext>>());
        eventService.Setup(x => x.Get(It.IsAny<int>(), It.IsAny<int>())).Throws(new InvalidOperationException("sensitive detail"));

        var controller = new EventsController(eventService.Object, new EventBus(eventService.Object));

        var result = controller.GetEvents();

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Equal("Failed to list events", error.Value);
    }

    [Fact]
    public void RaiseEvent_TrimsTypeBeforeParsing()
    {
        var eventService = new Mock<EventService>(Mock.Of<IDbContextFactory<EventsDbContext>>());
        var controller = new EventsController(eventService.Object, new EventBus(eventService.Object));

        var result = controller.RaiseEvent(" Noop ", " x ");

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public void RaiseEvent_WithUnknownType_ReturnsSanitizedBadRequest()
    {
        var eventService = new Mock<EventService>(Mock.Of<IDbContextFactory<EventsDbContext>>());
        var controller = new EventsController(eventService.Object, new EventBus(eventService.Object));

        var result = controller.RaiseEvent(" totally-not-real ", "x");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Unknown event type", badRequest.Value);
    }

    [Fact]
    public void RaiseEvent_WithReservedType_ReturnsSanitizedBadRequest()
    {
        var eventService = new Mock<EventService>(Mock.Of<IDbContextFactory<EventsDbContext>>());
        var controller = new EventsController(eventService.Object, new EventBus(eventService.Object));

        var result = controller.RaiseEvent(" Any ", "x");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Event type cannot be raised", badRequest.Value);
    }
}
