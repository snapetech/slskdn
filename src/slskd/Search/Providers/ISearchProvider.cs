// <copyright file="ISearchProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Search.Providers;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using slskd.Search;

/// <summary>
///     Interface for search providers (Pod/Mesh or Soulseek Scene).
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    ///     Gets the provider name (e.g., "pod" or "scene").
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Starts a search and streams results to the sink.
    /// </summary>
    /// <param name="request">The search request.</param>
    /// <param name="sink">The sink to receive search results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartSearchAsync(SearchRequest request, ISearchResultSink sink, CancellationToken ct);
}

/// <summary>
    ///     Sink interface for receiving search results from providers.
    /// </summary>
public interface ISearchResultSink
{
    /// <summary>
    ///     Adds a search result from a provider.
    /// </summary>
    /// <param name="result">The search result.</param>
    void AddResult(SearchResult result);
}

/// <summary>
    ///     Search request for providers.
    /// </summary>
public class SearchRequest
{
    /// <summary>
    ///     Gets or sets the search query text.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the search timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of responses to accept.
    /// </summary>
    public int? ResponseLimit { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of files to accept.
    /// </summary>
    public int? FileLimit { get; set; }
}

/// <summary>
    ///     Search result from a provider with provenance information.
    /// </summary>
public class SearchResult
{
    /// <summary>
    ///     Gets or sets the provider name (e.g., "pod" or "scene").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the source providers (can be multiple if result appears in both).
    /// </summary>
    public List<string> SourceProviders { get; set; } = new();

    /// <summary>
    ///     Gets or sets the primary source for action routing ("pod" or "scene").
    /// </summary>
    public string PrimarySource { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the search response (converted from provider-specific format).
    /// </summary>
    public Response Response { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the pod content reference (for pod results).
    /// </summary>
    public PodContentRef? PodContentRef { get; set; }

    /// <summary>
    ///     Gets or sets the scene content reference (for scene results).
    /// </summary>
    public SceneContentRef? SceneContentRef { get; set; }

    /// <summary>
    ///     Gets or sets the peer hint (for pod results, never exposed to scene).
    /// </summary>
    public string? PeerHint { get; set; }

    /// <summary>
    ///     Gets or sets the scene user hint (for scene results).
    /// </summary>
    public string? SceneUserHint { get; set; }
}

/// <summary>
    ///     Pod content reference.
    /// </summary>
public class PodContentRef
{
    /// <summary>
    ///     Gets or sets the content ID.
    /// </summary>
    public string ContentId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the optional hash.
    /// </summary>
    public string? Hash { get; set; }
}

/// <summary>
    ///     Scene content reference (Soulseek username + file tuple).
    /// </summary>
public class SceneContentRef
{
    /// <summary>
    ///     Gets or sets the Soulseek username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the filename.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the file size.
    /// </summary>
    public long Size { get; set; }
}
