// <copyright file="IdentityDbContext.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using Microsoft.EntityFrameworkCore;

/// <summary>SQLite DbContext for Contacts.</summary>
public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Contact> Contacts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Contact>(e =>
        {
            e.HasIndex(x => x.PeerId).IsUnique();
            e.HasIndex(x => x.Nickname);
        });
    }
}
