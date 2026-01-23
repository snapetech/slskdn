// <copyright file="SwarmJobModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Swarm;

/// <summary>
/// Swarm job model for orchestrated transfers.
/// </summary>
public record SwarmJob(string JobId, SwarmFile File, IReadOnlyList<SwarmSource> Sources);

public record SwarmFile(string ContentId, string Hash, long SizeBytes, string? Codec = null);

public record SwarmSource(string PeerId, string Transport, string? Address = null, int? Port = null);