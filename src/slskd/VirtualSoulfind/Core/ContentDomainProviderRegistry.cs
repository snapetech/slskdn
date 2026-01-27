// <copyright file="ContentDomainProviderRegistry.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
///     Registry for content domain providers.
/// </summary>
/// <remarks>
///     <para>
///         This registry manages all domain providers (built-in and custom/plugin),
///         allowing discovery and registration of custom domain providers at runtime.
///     </para>
///     <para>
///         The registry is thread-safe and supports:
///         - Registration of built-in providers (during DI setup)
///         - Registration of custom/plugin providers (at runtime)
///         - Provider lookup by domain
///         - Provider enumeration
///     </para>
/// </remarks>
public interface IContentDomainProviderRegistry
{
    /// <summary>
    ///     Registers a domain provider.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    /// <returns>True if registered successfully, false if a provider for this domain already exists.</returns>
    bool RegisterProvider(IContentDomainProvider provider);

    /// <summary>
    ///     Gets a provider for a specific domain.
    /// </summary>
    /// <param name="domain">The content domain.</param>
    /// <returns>The provider, or null if not found.</returns>
    IContentDomainProvider? GetProvider(ContentDomain domain);

    /// <summary>
    ///     Gets all registered providers.
    /// </summary>
    /// <returns>All registered providers.</returns>
    IReadOnlyList<IContentDomainProvider> GetAllProviders();

    /// <summary>
    ///     Gets all registered providers for a specific domain (if multiple providers exist).
    /// </summary>
    /// <param name="domain">The content domain.</param>
    /// <returns>All providers for the domain.</returns>
    IReadOnlyList<IContentDomainProvider> GetProviders(ContentDomain domain);

    /// <summary>
    ///     Unregisters a provider.
    /// </summary>
    /// <param name="domain">The domain of the provider to unregister.</param>
    /// <returns>True if unregistered, false if not found.</returns>
    bool UnregisterProvider(ContentDomain domain);
}

/// <summary>
///     Implementation of the content domain provider registry.
/// </summary>
public class ContentDomainProviderRegistry : IContentDomainProviderRegistry
{
    private readonly Dictionary<ContentDomain, IContentDomainProvider> _providers = new();
    private readonly object _lock = new();
    private readonly ILogger<ContentDomainProviderRegistry> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentDomainProviderRegistry"/> class.
    /// </summary>
    public ContentDomainProviderRegistry(ILogger<ContentDomainProviderRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool RegisterProvider(IContentDomainProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (_lock)
        {
            if (_providers.ContainsKey(provider.Domain))
            {
                _logger.LogWarning(
                    "[ContentDomainProviderRegistry] Provider for domain {Domain} already registered. Existing: {ExistingProvider}, New: {NewProvider}",
                    provider.Domain,
                    _providers[provider.Domain].GetType().Name,
                    provider.GetType().Name);
                return false;
            }

            _providers[provider.Domain] = provider;
            _logger.LogInformation(
                "[ContentDomainProviderRegistry] Registered provider {ProviderName} (v{Version}) for domain {Domain} (BuiltIn: {IsBuiltIn})",
                provider.DisplayName,
                provider.Version,
                provider.Domain,
                provider.IsBuiltIn);
            return true;
        }
    }

    /// <inheritdoc/>
    public IContentDomainProvider? GetProvider(ContentDomain domain)
    {
        lock (_lock)
        {
            return _providers.TryGetValue(domain, out var provider) ? provider : null;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IContentDomainProvider> GetAllProviders()
    {
        lock (_lock)
        {
            return _providers.Values.ToList();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IContentDomainProvider> GetProviders(ContentDomain domain)
    {
        lock (_lock)
        {
            var provider = GetProvider(domain);
            return provider != null ? new[] { provider } : Array.Empty<IContentDomainProvider>();
        }
    }

    /// <inheritdoc/>
    public bool UnregisterProvider(ContentDomain domain)
    {
        lock (_lock)
        {
            if (!_providers.TryGetValue(domain, out var provider))
            {
                return false;
            }

            _providers.Remove(domain);
            _logger.LogInformation(
                "[ContentDomainProviderRegistry] Unregistered provider {ProviderName} for domain {Domain}",
                provider.DisplayName,
                domain);
            return true;
        }
    }
}
