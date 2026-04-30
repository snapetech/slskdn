// <copyright file="IQuarantineJuryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.QuarantineJury;

public interface IQuarantineJuryService
{
    Task<QuarantineJuryValidationResult> CreateRequestAsync(
        QuarantineJuryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarantineJuryRequest>> GetRequestsAsync(CancellationToken cancellationToken = default);

    Task<QuarantineJuryRequest?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);

    Task<QuarantineJuryValidationResult> SubmitVerdictAsync(
        QuarantineJuryVerdictRecord verdict,
        CancellationToken cancellationToken = default);

    Task<QuarantineJuryAggregate> GetAggregateAsync(string requestId, CancellationToken cancellationToken = default);
}
