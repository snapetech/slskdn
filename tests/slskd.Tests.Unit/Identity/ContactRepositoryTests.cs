// <copyright file="ContactRepositoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity;

using System;
using System.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using slskd.Identity;
using Xunit;
using IDbContextFactory = Microsoft.EntityFrameworkCore.IDbContextFactory<slskd.Identity.IdentityDbContext>;

public class ContactRepositoryTests : IDisposable
{
    private readonly ContactRepository _repo;
    private readonly string _dbPath;
    private readonly string _connectionString;

    public ContactRepositoryTests()
    {
        // Use a temp file for SQLite
        _dbPath = Path.Combine(Path.GetTempPath(), $"ContactRepo_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        using var ctx = new IdentityDbContext(options);
        ctx.Database.EnsureCreated();
        var factory = new TestDbContextFactory(_connectionString);
        _repo = new ContactRepository(factory);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task AddAsync_CreatesContact()
    {
        var contact = new Contact { PeerId = "p1", Nickname = "Alice", Verified = true };
        var created = await _repo.AddAsync(contact, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("p1", created.PeerId);
        Assert.Equal("Alice", created.Nickname);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsContact()
    {
        var contact = new Contact { PeerId = "p1", Nickname = "Bob" };
        using var ctx = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options);
        ctx.Contacts.Add(contact);
        await ctx.SaveChangesAsync();

        var found = await _repo.GetByIdAsync(contact.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("p1", found.PeerId);
    }

    [Fact]
    public async Task GetByPeerIdAsync_ReturnsContact()
    {
        var contact = new Contact { PeerId = "p1", Nickname = "Charlie" };
        using var ctx = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options);
        ctx.Contacts.Add(contact);
        await ctx.SaveChangesAsync();

        var found = await _repo.GetByPeerIdAsync("p1", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Charlie", found.Nickname);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllContacts()
    {
        using var ctx = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options);
        ctx.Contacts.AddRange(
            new Contact { PeerId = "p1", Nickname = "A" },
            new Contact { PeerId = "p2", Nickname = "B" });
        await ctx.SaveChangesAsync();

        var all = await _repo.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContact()
    {
        var contact = new Contact { PeerId = "p1", Nickname = "Old" };
        using (var ctx = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options))
        {
            ctx.Contacts.Add(contact);
            await ctx.SaveChangesAsync();
        }

        contact.Nickname = "New";
        await _repo.UpdateAsync(contact, CancellationToken.None);

        using var ctx2 = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options);
        var updated = await ctx2.Contacts.FindAsync(contact.Id);
        Assert.Equal("New", updated!.Nickname);
    }

    [Fact]
    public async Task DeleteAsync_RemovesContact()
    {
        var contact = new Contact { PeerId = "p1", Nickname = "DeleteMe" };
        using (var ctx = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options))
        {
            ctx.Contacts.Add(contact);
            await ctx.SaveChangesAsync();
        }

        var deleted = await _repo.DeleteAsync(contact.Id, CancellationToken.None);

        Assert.True(deleted);
        using var ctx2 = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connectionString).Options);
        Assert.Null(await ctx2.Contacts.FindAsync(contact.Id));
    }

    private class TestDbContextFactory : IDbContextFactory<IdentityDbContext>
    {
        private readonly string _connectionString;

        public TestDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IdentityDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new IdentityDbContext(options);
        }

        public Task<IdentityDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
