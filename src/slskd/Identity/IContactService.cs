// <copyright file="IContactService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Service for managing contacts.</summary>
public interface IContactService
{
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default);
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contact?> GetByPeerIdAsync(string peerId, CancellationToken ct = default);
    Task<Contact> AddAsync(string peerId, string nickname, bool verified, CancellationToken ct = default);
    Task UpdateAsync(Contact contact, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
