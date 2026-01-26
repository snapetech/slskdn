// <copyright file="ShareGroupRepositoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Sharing;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using slskd.Sharing;
using Xunit;

public class ShareGroupRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<CollectionsDbContext> _factory;

    public ShareGroupRepositoryTests()
    {
        _dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new TestDbContextFactory(options);
        
        // Ensure database is created
        using var db = new CollectionsDbContext(options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_dbPath))
            System.IO.File.Delete(_dbPath);
    }

    [Fact]
    public async Task AddMemberByPeerIdAsync_CreatesMemberWithPeerId()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            await db.SaveChangesAsync();
        }

        await repo.AddMemberByPeerIdAsync(groupId, "peer123", default);

        using (var db = await _factory.CreateDbContextAsync())
        {
            var member = await db.ShareGroupMembers.FirstOrDefaultAsync(m => m.ShareGroupId == groupId && m.PeerId == "peer123");
            Assert.NotNull(member);
            Assert.Equal("peer123", member.PeerId);
            Assert.Equal("peer123", member.UserId); // UserId should be set to PeerId for backward compatibility
        }
    }

    [Fact]
    public async Task AddMemberByPeerIdAsync_Duplicate_DoesNotAddAgain()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            await db.SaveChangesAsync();
        }

        await repo.AddMemberByPeerIdAsync(groupId, "peer123", default);
        await repo.AddMemberByPeerIdAsync(groupId, "peer123", default);

        using (var db = await _factory.CreateDbContextAsync())
        {
            var count = await db.ShareGroupMembers.CountAsync(m => m.ShareGroupId == groupId && m.PeerId == "peer123");
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task RemoveMemberByPeerIdAsync_RemovesMember()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = groupId, UserId = "peer123", PeerId = "peer123" });
            await db.SaveChangesAsync();
        }

        await repo.RemoveMemberByPeerIdAsync(groupId, "peer123", default);

        using (var db = await _factory.CreateDbContextAsync())
        {
            var member = await db.ShareGroupMembers.FirstOrDefaultAsync(m => m.ShareGroupId == groupId && m.PeerId == "peer123");
            Assert.Null(member);
        }
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsAllMembers()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = groupId, UserId = "bob", PeerId = null });
            db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = groupId, UserId = "peer123", PeerId = "peer123" });
            await db.SaveChangesAsync();
        }

        var members = await repo.GetMembersAsync(groupId, default);

        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.PeerId == "peer123");
        Assert.Contains(members, m => m.PeerId == null && m.UserId == "bob");
    }

    [Fact]
    public async Task IsMemberByPeerIdAsync_ReturnsTrueWhenMember()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = groupId, UserId = "peer123", PeerId = "peer123" });
            await db.SaveChangesAsync();
        }

        var isMember = await repo.IsMemberByPeerIdAsync(groupId, "peer123", default);

        Assert.True(isMember);
    }

    [Fact]
    public async Task IsMemberByPeerIdAsync_ReturnsFalseWhenNotMember()
    {
        var repo = new ShareGroupRepository(_factory);
        var groupId = Guid.NewGuid();
        var group = new ShareGroup { Id = groupId, Name = "Test", OwnerUserId = "alice" };
        
        using (var db = await _factory.CreateDbContextAsync())
        {
            db.ShareGroups.Add(group);
            await db.SaveChangesAsync();
        }

        var isMember = await repo.IsMemberByPeerIdAsync(groupId, "peer123", default);

        Assert.False(isMember);
    }

    private class TestDbContextFactory : IDbContextFactory<CollectionsDbContext>
    {
        private readonly DbContextOptions<CollectionsDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CollectionsDbContext> options)
        {
            _options = options;
        }

        public CollectionsDbContext CreateDbContext() => new(_options);
    }
}
