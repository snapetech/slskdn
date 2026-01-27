// <copyright file="TrafficObserverIntegrationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Capture;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd;
using slskd.Events;
using Soulseek;
using OptionsModel = slskd.Options;

/// <summary>
/// Integrates TrafficObserver with EventBus for search and transfer events.
/// Phase 6A: T-803, T-804 - Integration with SearchService and TransferService.
/// </summary>
public class TrafficObserverIntegrationService
{
    private readonly ILogger<TrafficObserverIntegrationService> logger;
    private readonly ITrafficObserver trafficObserver;
    private readonly EventBus eventBus;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;

    public TrafficObserverIntegrationService(
        ILogger<TrafficObserverIntegrationService> logger,
        ITrafficObserver trafficObserver,
        EventBus eventBus,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.trafficObserver = trafficObserver;
        this.eventBus = eventBus;
        this.optionsMonitor = optionsMonitor;

        // Subscribe to events
        InitializeSubscriptions();
    }

    private void InitializeSubscriptions()
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Capture?.Enabled != true)
        {
            logger.LogDebug("[VSF-INTEGRATION] Traffic capture disabled, skipping event subscriptions");
            return;
        }

        // Subscribe to search responses
        eventBus.Subscribe<SearchResponsesReceivedEvent>(
            "TrafficObserverIntegrationService.SearchResponses",
            OnSearchResponsesReceivedAsync);

        // Subscribe to download completions
        eventBus.Subscribe<DownloadFileCompleteEvent>(
            "TrafficObserverIntegrationService.DownloadComplete",
            OnDownloadCompleteAsync);

        logger.LogInformation("[VSF-INTEGRATION] Subscribed to search and transfer events");
    }

    private async Task OnSearchResponsesReceivedAsync(SearchResponsesReceivedEvent evt)
    {
        try
        {
            // Note: SearchService now calls TrafficObserver directly with the query,
            // so this event handler is a backup for any other code paths that raise this event.
            // We don't have the query text here, so we use a placeholder.
            // In practice, SearchService integration handles this.
            
            foreach (var response in evt.Responses)
            {
                var query = "unknown"; // Placeholder - query not available in event
                await trafficObserver.OnSearchResultsAsync(query, response, default);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-INTEGRATION] Failed to process search responses event");
        }
    }

    private async Task OnDownloadCompleteAsync(DownloadFileCompleteEvent evt)
    {
        try
        {
            // Convert Transfer to the format expected by TrafficObserver
            // DownloadFileCompleteEvent contains a Transfer object
            var transfer = evt.Transfer;

            // TrafficObserver expects Transfers.Transfer, which is what we have
            await trafficObserver.OnTransferCompleteAsync(transfer, default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-INTEGRATION] Failed to process download complete event");
        }
    }
}
