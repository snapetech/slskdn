// <copyright file="MeshServicePublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Background service that periodically publishes local service descriptors to the DHT.
/// </summary>
public class MeshServicePublisher : BackgroundService
{
    private readonly ILogger<MeshServicePublisher> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly MeshServiceFabricOptions _options;
    private readonly ConcurrentDictionary<string, MeshServiceDescriptor> _localServices = new();

    public MeshServicePublisher(
        ILogger<MeshServicePublisher> logger,
        IMeshDhtClient dhtClient,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _options = options.Value;
    }

    /// <summary>
    /// Registers a local service for publishing.
    /// </summary>
    public void RegisterService(MeshServiceDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        _localServices[descriptor.ServiceId] = descriptor;
        _logger.LogInformation(
            "[ServicePublisher] Registered service: {ServiceName} (ID: {ServiceId})",
            descriptor.ServiceName, descriptor.ServiceId);
    }

    /// <summary>
    /// Unregisters a local service.
    /// </summary>
    public void UnregisterService(string serviceId)
    {
        if (_localServices.TryRemove(serviceId, out var descriptor))
        {
            _logger.LogInformation(
                "[ServicePublisher] Unregistered service: {ServiceName} (ID: {ServiceId})",
                descriptor.ServiceName, serviceId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        _logger.LogInformation("[ServicePublisher] Starting service publisher background service");

        // Wait a bit before first publish to allow services to register
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAllServicesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[ServicePublisher] Error publishing services");
            }

            // Wait for next publish interval
            await Task.Delay(
                TimeSpan.FromSeconds(_options.RepublishIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInformation("[ServicePublisher] Service publisher stopped");
    }

    private async Task PublishAllServicesAsync(CancellationToken cancellationToken)
    {
        if (_localServices.IsEmpty)
        {
            _logger.LogDebug("[ServicePublisher] No local services to publish");
            return;
        }

        _logger.LogInformation(
            "[ServicePublisher] Publishing {Count} local services to DHT",
            _localServices.Count);

        // Group services by ServiceName (DHT key)
        var servicesByName = _localServices.Values
            .GroupBy(d => d.ServiceName)
            .ToList();

        foreach (var group in servicesByName)
        {
            try
            {
                await PublishServiceGroupAsync(group.Key, group.ToList(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[ServicePublisher] Failed to publish service group: {ServiceName}",
                    group.Key);
            }
        }
    }

    private async Task PublishServiceGroupAsync(
        string serviceName,
        List<MeshServiceDescriptor> descriptors,
        CancellationToken cancellationToken)
    {
        try
        {
            // DHT key pattern: svc:<ServiceName>
            var dhtKey = $"svc:{serviceName}";

            // Serialize the list of descriptors
            var serialized = MessagePackSerializer.Serialize(descriptors);

            // Check size limit
            if (serialized.Length > _options.MaxDhtValueBytes)
            {
                _logger.LogWarning(
                    "[ServicePublisher] Serialized descriptors too large for {ServiceName}: {Size} > {Max}. " +
                    "Publishing only first {Count} descriptors.",
                    serviceName, serialized.Length, _options.MaxDhtValueBytes, descriptors.Count);

                // Try with fewer descriptors
                var reduced = descriptors.Take(descriptors.Count / 2).ToList();
                if (reduced.Any())
                {
                    serialized = MessagePackSerializer.Serialize(reduced);
                }
                else
                {
                    _logger.LogError(
                        "[ServicePublisher] Cannot reduce size for {ServiceName}, skipping",
                        serviceName);
                    return;
                }
            }

            // Publish to DHT
            await _dhtClient.PutAsync(
                dhtKey,
                serialized,
                _options.DescriptorTtlSeconds,
                cancellationToken);

            _logger.LogDebug(
                "[ServicePublisher] Published {Count} descriptor(s) for service: {ServiceName} (TTL: {Ttl}s)",
                descriptors.Count, serviceName, _options.DescriptorTtlSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ServicePublisher] Error publishing service group: {ServiceName}",
                serviceName);
        }
    }
}
