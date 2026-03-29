// <copyright file="UserNotesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Users;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Users.Notes;
using slskd.Users.Notes.API;
using Xunit;

public class UserNotesControllerTests
{
    [Fact]
    public async Task Get_TrimsUsernameBeforeDispatch()
    {
        var service = new Mock<IUserNoteService>();
        service
            .Setup(noteService => noteService.GetNoteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserNote { Username = "user1" });

        var controller = new UserNotesController(service.Object);

        await controller.Get(" user1 ", CancellationToken.None);

        service.Verify(noteService => noteService.GetNoteAsync("user1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_TrimsUsernameBeforeDispatch()
    {
        var service = new Mock<IUserNoteService>();
        service
            .Setup(noteService => noteService.SetNoteAsync(It.IsAny<UserNote>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNote note, CancellationToken _) => note);

        var controller = new UserNotesController(service.Object);

        await controller.Set(new UserNote { Username = " user1 ", Note = "note" }, CancellationToken.None);

        service.Verify(
            noteService => noteService.SetNoteAsync(
                It.Is<UserNote>(note => note.Username == "user1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WithBlankUsername_ReturnsBadRequest()
    {
        var controller = new UserNotesController(Mock.Of<IUserNoteService>());

        var result = await controller.Delete("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
