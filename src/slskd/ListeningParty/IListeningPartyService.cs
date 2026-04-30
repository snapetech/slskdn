// <copyright file="IListeningPartyService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty;

public interface IListeningPartyService
{
    Task<ListeningPartyEvent?> GetStateAsync(string podId, string channelId, CancellationToken cancellationToken = default);

    Task<ListeningPartyEvent?> GetStateByPartyIdAsync(string partyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListeningPartyAnnouncement>> ListDirectoryAsync(CancellationToken cancellationToken = default);

    Task<ListeningPartyEvent> PublishAsync(ListeningPartyEvent partyEvent, CancellationToken cancellationToken = default);
}
