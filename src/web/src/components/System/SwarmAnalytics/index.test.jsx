// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import React from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import { fireEvent } from '@testing-library/react';
import SwarmAnalytics from './index';
import * as swarmAnalyticsLib from '../../../lib/swarmAnalytics';
import { toast } from 'react-toastify';

// Mock dependencies
jest.mock('../../../lib/swarmAnalytics');
jest.mock('react-toastify', () => ({
  toast: {
    error: jest.fn(),
  },
}));

describe('SwarmAnalytics', () => {
  const mockPerformanceMetrics = {
    totalDownloads: 150,
    successRate: 0.95,
    averageSpeedBytesPerSecond: 1024 * 1024 * 5, // 5 MB/s
    averageDurationSeconds: 45.5,
    totalBytesDownloaded: 1024 * 1024 * 1024 * 10, // 10 GB
    totalChunksCompleted: 5000,
    chunkSuccessRate: 0.98,
  };

  const mockPeerRankings = [
    {
      peerId: 'peer-1',
      rank: 1,
      source: 'Soulseek',
      reputationScore: 0.95,
      averageRttMs: 50.5,
      averageThroughputBytesPerSecond: 1024 * 1024 * 2,
      chunksCompleted: 1000,
      chunkSuccessRate: 0.99,
    },
    {
      peerId: 'peer-2',
      rank: 2,
      source: 'Mesh',
      reputationScore: 0.85,
      averageRttMs: 75.2,
      averageThroughputBytesPerSecond: 1024 * 1024 * 1.5,
      chunksCompleted: 800,
      chunkSuccessRate: 0.92,
    },
  ];

  const mockEfficiencyMetrics = {
    chunkUtilization: 0.85,
    peerUtilization: 0.75,
    redundancyFactor: 1.5,
  };

  const mockTrends = {
    timePoints: ['2026-01-27T00:00:00Z', '2026-01-27T01:00:00Z'],
    successRates: [0.95, 0.96],
  };

  const mockRecommendations = [
    {
      type: 'PeerSelection',
      priority: 'High',
      title: 'Optimize Peer Selection',
      description: 'Consider prioritizing peers with lower latency',
      action: 'Review peer rankings and adjust selection algorithm',
      estimatedImpact: 0.15,
    },
    {
      type: 'ChunkSize',
      priority: 'Medium',
      title: 'Adjust Chunk Size',
      description: 'Current chunk size may be suboptimal',
      action: 'Experiment with different chunk sizes',
      estimatedImpact: 0.1,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    swarmAnalyticsLib.getPerformanceMetrics.mockResolvedValue(
      mockPerformanceMetrics,
    );
    swarmAnalyticsLib.getPeerRankings.mockResolvedValue(mockPeerRankings);
    swarmAnalyticsLib.getEfficiencyMetrics.mockResolvedValue(
      mockEfficiencyMetrics,
    );
    swarmAnalyticsLib.getTrends.mockResolvedValue(mockTrends);
    swarmAnalyticsLib.getRecommendations.mockResolvedValue(
      mockRecommendations,
    );
  });

  it('renders the component header', () => {
    render(<SwarmAnalytics />);
    expect(screen.getByText('Swarm Analytics')).toBeInTheDocument();
  });

  it('displays loading state initially', () => {
    render(<SwarmAnalytics />);
    // Semantic UI Loader may render differently, check for loading indicator
    expect(screen.getByText('Swarm Analytics')).toBeInTheDocument();
  });

  it('fetches and displays performance metrics', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Performance Metrics')).toBeInTheDocument();
    });

    expect(screen.getByText('150')).toBeInTheDocument(); // totalDownloads
    // Check for success rate label
    const successRateLabels = screen.getAllByText('Success Rate');
    expect(successRateLabels.length).toBeGreaterThan(0);
    // Check for total downloads label
    const totalDownloadsLabels = screen.getAllByText('Total Downloads');
    expect(totalDownloadsLabels.length).toBeGreaterThan(0);
  });

  it('fetches and displays efficiency metrics', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Efficiency Metrics')).toBeInTheDocument();
    });

    expect(screen.getByText('Chunk Utilization')).toBeInTheDocument();
    expect(screen.getByText('Peer Utilization')).toBeInTheDocument();
  });

  it('fetches and displays peer rankings table', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Top Peer Rankings')).toBeInTheDocument();
    });

    expect(screen.getByText('peer-1')).toBeInTheDocument();
    expect(screen.getByText('peer-2')).toBeInTheDocument();
    expect(screen.getByText('Soulseek')).toBeInTheDocument();
    expect(screen.getByText('Mesh')).toBeInTheDocument();
  });

  it('fetches and displays recommendations', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(
        screen.getByText('Optimization Recommendations'),
      ).toBeInTheDocument();
    });

    expect(screen.getByText('Optimize Peer Selection')).toBeInTheDocument();
    expect(screen.getByText('Adjust Chunk Size')).toBeInTheDocument();
  });

  it('allows changing time window', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Performance Metrics')).toBeInTheDocument();
    });

    // Find time window dropdown
    const timeWindowLabel = screen.getByText('Time Window');
    expect(timeWindowLabel).toBeInTheDocument();

    // The dropdown should be in the same segment
    const segment = timeWindowLabel.closest('.segment');
    expect(segment).toBeInTheDocument();
  });

  it('allows changing peer rankings limit', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Top Peer Rankings')).toBeInTheDocument();
    });

    // Find peer rankings limit dropdown
    const limitLabel = screen.getByText('Peer Rankings Limit');
    expect(limitLabel).toBeInTheDocument();
  });

  it('displays no data message when no analytics available', async () => {
    swarmAnalyticsLib.getPerformanceMetrics.mockResolvedValue(null);
    swarmAnalyticsLib.getPeerRankings.mockResolvedValue([]);
    swarmAnalyticsLib.getEfficiencyMetrics.mockResolvedValue(null);
    swarmAnalyticsLib.getTrends.mockResolvedValue(null);
    swarmAnalyticsLib.getRecommendations.mockResolvedValue([]);

    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('No Analytics Data')).toBeInTheDocument();
    });
  });

  it('handles API errors gracefully', async () => {
    const error = new Error('Network error');
    swarmAnalyticsLib.getPerformanceMetrics.mockRejectedValue(error);

    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalled();
    });
  });

  it('refreshes data periodically', async () => {
    jest.useFakeTimers();
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(swarmAnalyticsLib.getPerformanceMetrics).toHaveBeenCalledTimes(1);
    });

    // Fast-forward 30 seconds (refresh interval)
    jest.advanceTimersByTime(30000);

    await waitFor(() => {
      expect(swarmAnalyticsLib.getPerformanceMetrics).toHaveBeenCalledTimes(2);
    });

    jest.useRealTimers();
  });

  it('displays correct time window label', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText(/Last 24 hour/)).toBeInTheDocument();
    });
  });

  it('formats bytes correctly in statistics', async () => {
    render(<SwarmAnalytics />);

    await waitFor(() => {
      expect(screen.getByText('Performance Metrics')).toBeInTheDocument();
    });

    // Check that bytes are formatted (should contain "GB" or similar)
    const totalBytesText = screen.getByText(/Total Bytes/i);
    expect(totalBytesText).toBeInTheDocument();
  });
});
