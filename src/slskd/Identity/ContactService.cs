// <copyright file="ContactService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Implementation of IContactService. Delegates to IContactRepository.</summary>
public sealed class ContactService : IContactService
{
    private readonly IContactRepository _repo;

    public ContactService(IContactRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default) => _repo.GetAllAsync(ct);
    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<Contact?> GetByPeerIdAsync(string peerId, CancellationToken ct = default) => _repo.GetByPeerIdAsync(peerId, ct);

    public async Task<Contact> AddAsync(string peerId, string nickname, bool verified, CancellationToken ct = default)
    {
        var contact = new Contact
        {
            PeerId = peerId,
            Nickname = nickname,
            Verified = verified,
            CreatedAt = DateTimeOffset.UtcNow
        };
        return await _repo.AddAsync(contact, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Contact contact, CancellationToken ct = default) => _repo.UpdateAsync(contact, ct);
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
