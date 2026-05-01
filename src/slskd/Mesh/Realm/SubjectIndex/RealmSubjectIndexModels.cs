// <copyright file="RealmSubjectIndexModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex;

using System.Security.Cryptography;
using System.Text;
using slskd.SocialFederation;

public sealed class RealmSubjectIndex
{
    public string Id { get; set; } = string.Empty;

    public string RealmId { get; set; } = string.Empty;

    public string SubjectNamespace { get; set; } = "music";

    public int Revision { get; set; } = 1;

    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<RealmSubjectIndexEntry> Entries { get; set; } = new();

    public RealmSubjectIndexSignature Signature { get; set; } = new();

    public string ComputePayloadHash()
    {
        var canonical = new StringBuilder()
            .Append(RealmId.Trim().ToLowerInvariant()).Append('|')
            .Append(SubjectNamespace.Trim().ToLowerInvariant()).Append('|')
            .Append(Revision).Append('|');

        foreach (var entry in Entries.OrderBy(entry => entry.SubjectId, StringComparer.OrdinalIgnoreCase))
        {
            canonical
                .Append(entry.SubjectId.Trim().ToLowerInvariant()).Append(':')
                .Append(entry.WorkRef.Domain.Trim().ToLowerInvariant()).Append(':')
                .Append(entry.WorkRef.Title.Trim().ToLowerInvariant()).Append(':')
                .Append(entry.WorkRef.Creator?.Trim().ToLowerInvariant() ?? string.Empty).Append(':')
                .Append(string.Join(',', entry.ExternalIds
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => $"{pair.Key.Trim().ToLowerInvariant()}={pair.Value.Trim().ToLowerInvariant()}"))).Append(':')
                .Append(string.Join(',', entry.Aliases.Select(alias => alias.Trim().ToLowerInvariant()).Order(StringComparer.OrdinalIgnoreCase))).Append(':')
                .Append(string.Join(',', entry.EvidenceLinks.Select(link => link.Trim().ToLowerInvariant()).Order(StringComparer.OrdinalIgnoreCase))).Append('|');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}

public sealed class RealmSubjectIndexEntry
{
    public string SubjectId { get; set; } = string.Empty;

    public WorkRef WorkRef { get; set; } = new();

    public Dictionary<string, string> ExternalIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Aliases { get; set; } = new();

    public List<string> EvidenceLinks { get; set; } = new();

    public string Note { get; set; } = string.Empty;
}

public sealed class RealmSubjectIndexSignature
{
    public string Signer { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "realm-governance-sha256";

    public string PayloadHash { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class RealmSubjectIndexValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; set; } = new();
}

public sealed class RealmSubjectIndexResolution
{
    public RealmSubjectIndexEntry Entry { get; set; } = new();

    public string RealmId { get; set; } = string.Empty;

    public string IndexId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string Provenance { get; set; } = string.Empty;
}

public sealed class RealmSubjectIndexConflictReport
{
    public string RealmId { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public int IndexCount { get; set; }

    public int DisabledAuthorityCount { get; set; }

    public int EntryCount { get; set; }

    public bool HasConflicts => Conflicts.Count > 0;

    public List<RealmSubjectIndexConflict> Conflicts { get; set; } = new();
}

public sealed class RealmSubjectIndexAuthorityDecisionRequest
{
    public bool Enabled { get; set; }

    public string DecidedBy { get; set; } = "local-user";

    public string Note { get; set; } = string.Empty;
}

public sealed class RealmSubjectIndexAuthorityDecision
{
    public bool IsAccepted => Errors.Count == 0;

    public string RealmId { get; set; } = string.Empty;

    public string IndexId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string DecidedBy { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<string> Errors { get; set; } = new();
}

public sealed class RealmSubjectIndexConflict
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string SubjectNamespace { get; set; } = string.Empty;

    public string SubjectId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<RealmSubjectIndexConflictValue> Values { get; set; } = new();
}

public sealed class RealmSubjectIndexConflictValue
{
    public string RealmId { get; set; } = string.Empty;

    public string IndexId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string SubjectId { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string WorkTitle { get; set; } = string.Empty;

    public string WorkCreator { get; set; } = string.Empty;

    public List<string> Aliases { get; set; } = new();

    public Dictionary<string, string> ExternalIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string AuthorityKey { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;
}

public sealed class RealmSubjectIndexProposal
{
    public string ProposalId { get; set; } = string.Empty;

    public string RealmId { get; set; } = string.Empty;

    public string IndexId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public RealmSubjectIndexProposalStatus Status { get; set; } = RealmSubjectIndexProposalStatus.Pending;

    public DateTimeOffset ProposedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ProposedBy { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string GovernanceDocumentId { get; set; } = string.Empty;

    public RealmSubjectIndex Index { get; set; } = new();

    public RealmSubjectIndexProposalReview? Review { get; set; }

    public List<string> Errors { get; set; } = new();
}

public enum RealmSubjectIndexProposalStatus
{
    Pending,
    Accepted,
    Rejected,
    Failed,
}

public sealed class RealmSubjectIndexProposalReview
{
    public string ProposalId { get; set; } = string.Empty;

    public string ReviewedBy { get; set; } = string.Empty;

    public bool Accept { get; set; }

    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Note { get; set; } = string.Empty;

    public string GovernanceDocumentId { get; set; } = string.Empty;

    public List<string> Errors { get; set; } = new();
}
