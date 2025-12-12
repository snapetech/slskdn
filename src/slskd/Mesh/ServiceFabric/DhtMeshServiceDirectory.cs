using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// DHT-backed implementation of IMeshServiceDirectory.
/// Uses DHT key pattern: svc:&lt;ServiceName&gt;
/// </summary>
public class DhtMeshServiceDirectory : IMeshServiceDirectory
{
    private readonly ILogger<DhtMeshServiceDirectory> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IMeshServiceDescriptorValidator _validator;
    private readonly MeshServiceFabricOptions _options;

    public DhtMeshServiceDirectory(
        ILogger<DhtMeshServiceDirectory> logger,
        IMeshDhtClient dhtClient,
        IMeshServiceDescriptorValidator validator,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _validator = validator;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogWarning("[ServiceDirectory] FindByName called with empty service name");
            return Array.Empty<MeshServiceDescriptor>();
        }

        try
        {
            var dhtKey = $"svc:{serviceName}";
            var rawValue = await _dhtClient.GetRawAsync(dhtKey, cancellationToken);

            if (rawValue == null || rawValue.Length == 0)
            {
                _logger.LogDebug("[ServiceDirectory] No DHT value found for service: {ServiceName}", serviceName);
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Check DHT value size limit
            if (rawValue.Length > _options.MaxDhtValueBytes)
            {
                _logger.LogWarning(
                    "[ServiceDirectory] DHT value too large for {ServiceName}: {Size} > {Max}",
                    serviceName, rawValue.Length, _options.MaxDhtValueBytes);
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Deserialize list of descriptors
            List<MeshServiceDescriptor> descriptors;
            try
            {
                descriptors = MessagePackSerializer.Deserialize<List<MeshServiceDescriptor>>(rawValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ServiceDirectory] Failed to deserialize descriptors for {ServiceName}", serviceName);
                return Array.Empty<MeshServiceDescriptor>();
            }

            if (descriptors == null || descriptors.Count == 0)
            {
                return Array.Empty<MeshServiceDescriptor>();
            }

            // Validate and filter descriptors
            var validated = new List<MeshServiceDescriptor>();
            foreach (var descriptor in descriptors)
            {
                if (!_validator.Validate(descriptor, out var reason))
                {
                    _logger.LogDebug(
                        "[ServiceDirectory] Invalid descriptor for {ServiceName}: {Reason}",
                        serviceName, reason);
                    continue;
                }

                validated.Add(descriptor);

                // Stop at max limit
                if (validated.Count >= _options.MaxDescriptorsPerLookup)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "[ServiceDirectory] Found {Count} valid descriptors for service: {ServiceName}",
                validated.Count, serviceName);

            return validated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServiceDirectory] Error finding service by name: {ServiceName}", serviceName);
            return Array.Empty<MeshServiceDescriptor>();
        }
    }

    public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            _logger.LogWarning("[ServiceDirectory] FindById called with empty service ID");
            return Array.Empty<MeshServiceDescriptor>();
        }

        try
        {
            // Service ID format: hash("svc:" + ServiceName + ":" + OwnerPeerId)
            // We need to query DHT by scanning service names or use a reverse index
            // For now, this is a stub - full implementation would require additional DHT structures

            _logger.LogDebug("[ServiceDirectory] FindById not yet fully implemented: {ServiceId}", serviceId);

            // TODO: Implement efficient FindById
            // Options:
            // 1. Maintain a separate DHT key: svcid:<ServiceId> -> descriptor
            // 2. Scan known service names (inefficient but works for now)
            // 3. Use DHT FindValue with serviceId directly

            return Array.Empty<MeshServiceDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServiceDirectory] Error finding service by ID: {ServiceId}", serviceId);
            return Array.Empty<MeshServiceDescriptor>();
        }
    }
}
