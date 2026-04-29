// <copyright file="UserNotesDbContext.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Users.Notes
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     Data context for user notes.
    /// </summary>
    public class UserNotesDbContext : DbContext
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserNotesDbContext"/> class.
        /// </summary>
        /// <param name="options">The options for this context.</param>
        public UserNotesDbContext(DbContextOptions<UserNotesDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        ///     Gets or sets the user notes.
        /// </summary>
        public DbSet<UserNote> UserNotes { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserNote>()
                .HasKey(x => x.Username);
        }
    }
}
