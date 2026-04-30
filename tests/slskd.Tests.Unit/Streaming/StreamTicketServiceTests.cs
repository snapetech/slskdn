// <copyright file="StreamTicketServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Streaming;

using System;
using slskd.Streaming;
using Xunit;

public class StreamTicketServiceTests
{
    [Fact]
    public void Validate_WithMatchingContentId_ReturnsClaims()
    {
        var service = new StreamTicketService();
        var ticket = service.Create("content-1", "user:alice", TimeSpan.FromMinutes(1));

        var claims = service.Validate(ticket, "content-1");

        Assert.NotNull(claims);
        Assert.Equal("content-1", claims.ContentId);
        Assert.Equal("user:alice", claims.OwnerKey);
    }

    [Fact]
    public void Validate_WithDifferentContentId_ReturnsNull()
    {
        var service = new StreamTicketService();
        var ticket = service.Create("content-1", "user:alice", TimeSpan.FromMinutes(1));

        var claims = service.Validate(ticket, "content-2");

        Assert.Null(claims);
    }

    [Fact]
    public void Validate_WithExpiredTicket_ReturnsNull()
    {
        var service = new StreamTicketService();
        var ticket = service.Create("content-1", "user:alice", TimeSpan.FromSeconds(-1));

        var claims = service.Validate(ticket, "content-1");

        Assert.Null(claims);
    }
}
