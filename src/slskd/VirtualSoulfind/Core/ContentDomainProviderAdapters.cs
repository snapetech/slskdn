// <copyright file="ContentDomainProviderAdapters.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core.Book;
using slskd.VirtualSoulfind.Core.GenericFile;
using slskd.VirtualSoulfind.Core.Movie;
using slskd.VirtualSoulfind.Core.Music;
using slskd.VirtualSoulfind.Core.Tv;
using slskd.VirtualSoulfind.v2.Backends;

/// <summary>
///     Adapter classes that wrap existing domain-specific providers to implement IContentDomainProvider.
/// </summary>
/// <remarks>
///     These adapters allow existing domain-specific providers (IMusicContentDomainProvider, etc.)
///     to be used with the IContentDomainProvider registry system without requiring changes
///     to the existing provider implementations.
/// </remarks>
public static class ContentDomainProviderAdapters
{
    /// <summary>
    ///     Creates an IContentDomainProvider adapter for a music domain provider.
    /// </summary>
    public static IContentDomainProvider CreateMusicAdapter(IMusicContentDomainProvider provider)
    {
        return new MusicDomainProviderAdapter(provider);
    }

    /// <summary>
    ///     Creates an IContentDomainProvider adapter for a book domain provider.
    /// </summary>
    public static IContentDomainProvider CreateBookAdapter(IBookContentDomainProvider provider)
    {
        return new BookDomainProviderAdapter(provider);
    }

    /// <summary>
    ///     Creates an IContentDomainProvider adapter for a movie domain provider.
    /// </summary>
    public static IContentDomainProvider CreateMovieAdapter(IMovieContentDomainProvider provider)
    {
        return new MovieDomainProviderAdapter(provider);
    }

    /// <summary>
    ///     Creates an IContentDomainProvider adapter for a TV domain provider.
    /// </summary>
    public static IContentDomainProvider CreateTvAdapter(ITvContentDomainProvider provider)
    {
        return new TvDomainProviderAdapter(provider);
    }

    /// <summary>
    ///     Creates an IContentDomainProvider adapter for a GenericFile domain provider.
    /// </summary>
    public static IContentDomainProvider CreateGenericFileAdapter(IGenericFileContentDomainProvider provider)
    {
        return new GenericFileDomainProviderAdapter(provider);
    }

    private class MusicDomainProviderAdapter : IContentDomainProvider
    {
        private readonly IMusicContentDomainProvider _provider;
        private static readonly IReadOnlyList<ContentBackendType> _supportedBackends = new[]
        {
            ContentBackendType.Soulseek,
            ContentBackendType.MeshDht,
            ContentBackendType.Torrent,
            ContentBackendType.Http,
            ContentBackendType.LocalLibrary,
        };

        public MusicDomainProviderAdapter(IMusicContentDomainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ContentDomain Domain => ContentDomain.Music;
        public string DisplayName => "Music Domain Provider";
        public string Version => "1.0.0";
        public bool IsBuiltIn => true;
        public IReadOnlyList<ContentBackendType> SupportedBackendTypes => _supportedBackends;

        public async Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByReleaseIdAsync(identifier, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByTitleArtistAsync(title, creator ?? string.Empty, year, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByRecordingIdAsync(identifier, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default)
        {
            // Music domain doesn't have a direct hash lookup - would need to search HashDb
            return null;
        }

        public async Task<IContentItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // Music domain requires AudioTags - this is a limitation of the adapter
            // In practice, this would need to extract tags from the file
            return null;
        }

        public Task<bool> VerifyItemMatchAsync(ContentItemId itemId, LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // Music domain verification would require AudioTags
            return Task.FromResult(false);
        }
    }

    private class BookDomainProviderAdapter : IContentDomainProvider
    {
        private readonly IBookContentDomainProvider _provider;
        private static readonly IReadOnlyList<ContentBackendType> _supportedBackends = new[]
        {
            ContentBackendType.MeshDht,
            ContentBackendType.Torrent,
            ContentBackendType.Http,
            ContentBackendType.LocalLibrary,
        };

        public BookDomainProviderAdapter(IBookContentDomainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ContentDomain Domain => ContentDomain.Book;
        public string DisplayName => "Book Domain Provider";
        public string Version => "1.0.0";
        public bool IsBuiltIn => true;
        public IReadOnlyList<ContentBackendType> SupportedBackendTypes => _supportedBackends;

        public async Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByIsbnAsync(identifier, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByTitleAuthorAsync(title, creator ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            // Book domain doesn't have item-level identifiers beyond ISBN
            return null;
        }

        public async Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByHashAsync(hash, filename ?? string.Empty, sizeBytes, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByLocalMetadataAsync(fileMetadata, cancellationToken).ConfigureAwait(false);
        }

        public Task<bool> VerifyItemMatchAsync(ContentItemId itemId, LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // Book domain verification would check hash and size
            return Task.FromResult(false);
        }
    }

    private class MovieDomainProviderAdapter : IContentDomainProvider
    {
        private readonly IMovieContentDomainProvider _provider;
        private static readonly IReadOnlyList<ContentBackendType> _supportedBackends = new[]
        {
            ContentBackendType.MeshDht,
            ContentBackendType.Torrent,
            ContentBackendType.Http,
            ContentBackendType.LocalLibrary,
        };

        public MovieDomainProviderAdapter(IMovieContentDomainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ContentDomain Domain => ContentDomain.Movie;
        public string DisplayName => "Movie Domain Provider";
        public string Version => "1.0.0";
        public bool IsBuiltIn => true;
        public IReadOnlyList<ContentBackendType> SupportedBackendTypes => _supportedBackends;

        public async Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByImdbIdAsync(identifier, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByTitleYearAsync(title, year, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            // Movie domain doesn't have item-level identifiers beyond IMDB ID
            return null;
        }

        public async Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByHashAsync(hash, filename ?? string.Empty, sizeBytes, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByLocalMetadataAsync(fileMetadata, cancellationToken).ConfigureAwait(false);
        }

        public Task<bool> VerifyItemMatchAsync(ContentItemId itemId, LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // Movie domain verification would check hash and size
            return Task.FromResult(false);
        }
    }

    private class TvDomainProviderAdapter : IContentDomainProvider
    {
        private readonly ITvContentDomainProvider _provider;
        private static readonly IReadOnlyList<ContentBackendType> _supportedBackends = new[]
        {
            ContentBackendType.MeshDht,
            ContentBackendType.Torrent,
            ContentBackendType.Http,
            ContentBackendType.LocalLibrary,
        };

        public TvDomainProviderAdapter(ITvContentDomainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ContentDomain Domain => ContentDomain.Tv;
        public string DisplayName => "TV Domain Provider";
        public string Version => "1.0.0";
        public bool IsBuiltIn => true;
        public IReadOnlyList<ContentBackendType> SupportedBackendTypes => _supportedBackends;

        public async Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByTvdbIdAsync(identifier, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetWorkByTitleAsync(title, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            // TV domain doesn't have item-level identifiers beyond TVDB ID
            return null;
        }

        public async Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByHashAsync(hash, filename ?? string.Empty, sizeBytes, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByLocalMetadataAsync(fileMetadata, cancellationToken).ConfigureAwait(false);
        }

        public Task<bool> VerifyItemMatchAsync(ContentItemId itemId, LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // TV domain verification would check hash and size
            return Task.FromResult(false);
        }
    }

    private class GenericFileDomainProviderAdapter : IContentDomainProvider
    {
        private readonly IGenericFileContentDomainProvider _provider;
        private static readonly IReadOnlyList<ContentBackendType> _supportedBackends = new[]
        {
            ContentBackendType.MeshDht,
            ContentBackendType.Torrent,
            ContentBackendType.Http,
            ContentBackendType.LocalLibrary,
        };

        public GenericFileDomainProviderAdapter(IGenericFileContentDomainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ContentDomain Domain => ContentDomain.GenericFile;
        public string DisplayName => "Generic File Domain Provider";
        public string Version => "1.0.0";
        public bool IsBuiltIn => true;
        public IReadOnlyList<ContentBackendType> SupportedBackendTypes => _supportedBackends;

        public Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            // GenericFile domain doesn't have works
            return Task.FromResult<IContentWork?>(null);
        }

        public Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default)
        {
            // GenericFile domain doesn't have works
            return Task.FromResult<IContentWork?>(null);
        }

        public Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        {
            // GenericFile domain doesn't have item-level identifiers
            return Task.FromResult<IContentItem?>(null);
        }

        public async Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByHashAndFilenameAsync(hash, filename ?? string.Empty, sizeBytes, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IContentItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            return await _provider.TryGetItemByLocalMetadataAsync(fileMetadata, cancellationToken).ConfigureAwait(false);
        }

        public Task<bool> VerifyItemMatchAsync(ContentItemId itemId, LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            // GenericFile domain verification would check hash and size
            return Task.FromResult(false);
        }
    }
}
