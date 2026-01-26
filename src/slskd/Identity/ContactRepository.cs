// <copyright file="ContactRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of IContactRepository.</summary>
public sealed class ContactRepository : IContactRepository
{
    private readonly IDbContextFactory<IdentityDbContext> _factory;

    public ContactRepository(IDbContextFactory<IdentityDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.Contacts.OrderBy(x => x.Nickname).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.Contacts.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
    }

    public async Task<Contact?> GetByPeerIdAsync(string peerId, CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.Contacts.FirstOrDefaultAsync(x => x.PeerId == peerId, ct).ConfigureAwait(false);
    }

    public async Task<Contact> AddAsync(Contact contact, CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        ctx.Contacts.Add(contact);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return contact;
    }

    public async Task UpdateAsync(Contact contact, CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        ctx.Contacts.Update(contact);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var c = await ctx.Contacts.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (c == null) return false;
        ctx.Contacts.Remove(c);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
