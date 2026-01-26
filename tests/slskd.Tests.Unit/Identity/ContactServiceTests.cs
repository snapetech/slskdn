// <copyright file="ContactServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using slskd.Identity;
using Xunit;

public class ContactServiceTests
{
    private readonly Mock<IContactRepository> _repoMock = new();

    private ContactService CreateService()
    {
        return new ContactService(_repoMock.Object);
    }

    [Fact]
    public async Task GetAll_DelegatesToRepository()
    {
        var svc = CreateService();
        var expected = new List<Contact> { new() { PeerId = "p1", Nickname = "Alice" } };
        _repoMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await svc.GetAllAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("p1", result[0].PeerId);
    }

    [Fact]
    public async Task AddAsync_CreatesContact()
    {
        var svc = CreateService();
        var created = new Contact { Id = Guid.NewGuid(), PeerId = "p1", Nickname = "Bob", Verified = true };
        _repoMock.Setup(x => x.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await svc.AddAsync("p1", "Bob", true, CancellationToken.None);

        Assert.Equal("p1", result.PeerId);
        Assert.Equal("Bob", result.Nickname);
        Assert.True(result.Verified);
        _repoMock.Verify(x => x.AddAsync(It.Is<Contact>(c => c.PeerId == "p1" && c.Nickname == "Bob"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DelegatesToRepository()
    {
        var svc = CreateService();
        var contact = new Contact { Id = Guid.NewGuid(), PeerId = "p1", Nickname = "Charlie" };

        await svc.UpdateAsync(contact, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(contact, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        _repoMock.Setup(x => x.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await svc.DeleteAsync(id, CancellationToken.None);

        Assert.True(result);
    }
}
