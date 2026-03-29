// <copyright file="LogsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Core.API;

using Microsoft.AspNetCore.Mvc;
using slskd.Core.API;
using Xunit;

public class LogsControllerTests
{
    [Fact]
    public void Logs_ReturnsSnapshotArray()
    {
        Program.LogBuffer.Enqueue(new LogRecord
        {
            Context = "test",
            Message = "message",
            Timestamp = DateTime.UtcNow,
        });

        var controller = new LogsController();

        var result = Assert.IsType<OkObjectResult>(controller.Logs());

        Assert.IsAssignableFrom<LogRecord[]>(result.Value);
    }
}
