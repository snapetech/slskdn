// <copyright file="NowPlayingControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.NowPlaying;

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using slskd.NowPlaying;
using slskd.NowPlaying.API;
using Xunit;

public class NowPlayingControllerTests
{
    [Fact]
    public void Put_Trims_Track_Fields_Before_Setting()
    {
        var service = new NowPlayingService();
        var controller = new NowPlayingController(service);

        var result = controller.Put(new NowPlayingRequest
        {
            Artist = " artist ",
            Title = " title ",
            Album = " album ",
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("artist", service.CurrentTrack?.Artist);
        Assert.Equal("title", service.CurrentTrack?.Title);
        Assert.Equal("album", service.CurrentTrack?.Album);
    }

    [Fact]
    public void Put_With_Blank_Artist_Returns_BadRequest()
    {
        var controller = new NowPlayingController(new NowPlayingService());

        var result = controller.Put(new NowPlayingRequest
        {
            Artist = "   ",
            Title = "title",
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Webhook_Trims_Generic_Event_Before_Routing()
    {
        var service = new NowPlayingService();
        var controller = new NowPlayingController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        using var body = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "event": " stop ", "artist": "Artist", "title": "Title" }
            """));
        controller.Request.Body = body;

        service.SetTrack("Artist", "Title", null);

        var result = await controller.Webhook();

        Assert.IsType<OkResult>(result);
        Assert.Null(service.CurrentTrack);
    }
}
