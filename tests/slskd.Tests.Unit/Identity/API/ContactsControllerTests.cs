// <copyright file="ContactsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity.API;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Identity;
using slskd.Identity.API;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class ContactsControllerTests
{
    private readonly Mock<IContactService> _contactsMock = new();
    private readonly Mock<IProfileService> _profileMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { IdentityFriends = true }
    });

    private ContactsController CreateController()
    {
        var c = new ContactsController(_contactsMock.Object, _profileMock.Object, _options);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
        return c;
    }

    [Fact]
    public async Task GetAll_FeatureDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { IdentityFriends = false }
        });
        var c = CreateController();

        var r = await c.GetAll(CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task GetAll_Success_ReturnsList()
    {
        var c = CreateController();
        var list = new List<Contact> { new() { Id = Guid.NewGuid(), PeerId = "p1", Nickname = "Alice" } };
        _contactsMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var r = await c.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var result = Assert.IsAssignableFrom<List<Contact>>(ok.Value);
        Assert.Single(result);
    }

    [Fact]
    public async Task AddFromInvite_InvalidLink_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.AddFromInvite(new AddFromInviteRequest { InviteLink = "invalid", Nickname = "Bob" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Contains("Failed to decode", bad.Value?.ToString());
    }

    [Fact]
    public async Task AddFromInvite_ExpiredInvite_ReturnsBadRequest()
    {
        var c = CreateController();
        var invite = new FriendInvite
        {
            Profile = new PeerProfile { PeerId = "p1", DisplayName = "Test", PublicKey = "key", Signature = "sig" },
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        var json = JsonSerializer.Serialize(invite);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"slskdn://invite/{base64}";

        var r = await c.AddFromInvite(new AddFromInviteRequest { InviteLink = link, Nickname = "Bob" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("Invite expired.", bad.Value);
    }

    [Fact]
    public async Task AddFromInvite_ValidInvite_Success()
    {
        var c = CreateController();
        // Create a properly signed profile for the test
        var profile = new PeerProfile 
        { 
            PeerId = "p1", 
            DisplayName = "Test", 
            PublicKey = Convert.ToBase64String(new byte[32]), 
            Signature = Convert.ToBase64String(new byte[64]),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        var invite = new FriendInvite
        {
            Profile = profile,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        var json = JsonSerializer.Serialize(invite);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"slskdn://invite/{base64}";
        var contact = new Contact { Id = Guid.NewGuid(), PeerId = "p1", Nickname = "Bob" };

        _profileMock.Setup(x => x.VerifyProfile(It.IsAny<PeerProfile>())).Returns(true);
        _contactsMock.Setup(x => x.AddAsync("p1", "Bob", true, It.IsAny<CancellationToken>())).ReturnsAsync(contact);
        _contactsMock.Setup(x => x.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var r = await c.AddFromInvite(new AddFromInviteRequest { InviteLink = link, Nickname = "Bob" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(r);
        Assert.Equal(contact, created.Value);
    }

    [Fact]
    public async Task AddFromDiscovery_ProfileNotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _profileMock.Setup(x => x.GetProfileAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync((PeerProfile?)null);

        var r = await c.AddFromDiscovery(new AddFromDiscoveryRequest { PeerId = "p1", Nickname = "Alice" }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(r);
    }

    [Fact]
    public async Task Update_Success_ReturnsUpdated()
    {
        var c = CreateController();
        var contact = new Contact { Id = Guid.NewGuid(), PeerId = "p1", Nickname = "Old" };
        _contactsMock.Setup(x => x.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var r = await c.Update(contact.Id, new UpdateContactRequest { Nickname = "New" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var updated = Assert.IsType<Contact>(ok.Value);
        Assert.Equal("New", updated.Nickname);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _contactsMock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var r = await c.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }
}
