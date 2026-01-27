// <copyright file="swarmAnalytics.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';
import * as swarmAnalytics from './swarmAnalytics';

// Mock the api module
jest.mock('./api', () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

describe('swarmAnalytics', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getPerformanceMetrics', () => {
    it('calls API with default time window', async () => {
      const mockMetrics = {
        averageSpeedBytesPerSecond: 1_024 * 1_024,
        successRate: 0.95,
        totalDownloads: 100,
      };
      api.get.mockResolvedValue({ data: mockMetrics });

      const result = await swarmAnalytics.getPerformanceMetrics();

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/performance?timeWindowHours=24',
      );
      expect(result).toEqual(mockMetrics);
    });

    it('calls API with custom time window', async () => {
      const mockMetrics = { totalDownloads: 50 };
      api.get.mockResolvedValue({ data: mockMetrics });

      await swarmAnalytics.getPerformanceMetrics(6);

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/performance?timeWindowHours=6',
      );
    });

    it('handles API errors', async () => {
      const error = new Error('Network error');
      api.get.mockRejectedValue(error);

      await expect(swarmAnalytics.getPerformanceMetrics()).rejects.toThrow(
        'Network error',
      );
    });
  });

  describe('getPeerRankings', () => {
    it('calls API with default limit', async () => {
      const mockRankings = [{ peerId: 'peer1', rank: 1, reputationScore: 0.9 }];
      api.get.mockResolvedValue({ data: mockRankings });

      const result = await swarmAnalytics.getPeerRankings();

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/peers/rankings?limit=20',
      );
      expect(result).toEqual(mockRankings);
    });

    it('calls API with custom limit', async () => {
      const mockRankings = [];
      api.get.mockResolvedValue({ data: mockRankings });

      await swarmAnalytics.getPeerRankings(50);

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/peers/rankings?limit=50',
      );
    });

    it('handles API errors', async () => {
      const error = new Error('API error');
      api.get.mockRejectedValue(error);

      await expect(swarmAnalytics.getPeerRankings()).rejects.toThrow(
        'API error',
      );
    });
  });

  describe('getEfficiencyMetrics', () => {
    it('calls API with default time window', async () => {
      const mockMetrics = {
        chunkUtilization: 0.85,
        peerUtilization: 0.75,
      };
      api.get.mockResolvedValue({ data: mockMetrics });

      const result = await swarmAnalytics.getEfficiencyMetrics();

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/efficiency?timeWindowHours=24',
      );
      expect(result).toEqual(mockMetrics);
    });

    it('calls API with custom time window', async () => {
      const mockMetrics = {};
      api.get.mockResolvedValue({ data: mockMetrics });

      await swarmAnalytics.getEfficiencyMetrics(12);

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/efficiency?timeWindowHours=12',
      );
    });
  });

  describe('getTrends', () => {
    it('calls API with default parameters', async () => {
      const mockTrends = {
        successRates: [],
        timePoints: [],
      };
      api.get.mockResolvedValue({ data: mockTrends });

      const result = await swarmAnalytics.getTrends();

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/trends?timeWindowHours=24&dataPoints=24',
      );
      expect(result).toEqual(mockTrends);
    });

    it('calls API with custom parameters', async () => {
      const mockTrends = {};
      api.get.mockResolvedValue({ data: mockTrends });

      await swarmAnalytics.getTrends(12, 12);

      expect(api.get).toHaveBeenCalledWith(
        '/swarm/analytics/trends?timeWindowHours=12&dataPoints=12',
      );
    });
  });

  describe('getRecommendations', () => {
    it('calls API and returns recommendations', async () => {
      const mockRecommendations = [
        {
          priority: 'High',
          title: 'Optimize Peer Selection',
          type: 'PeerSelection',
        },
      ];
      api.get.mockResolvedValue({ data: mockRecommendations });

      const result = await swarmAnalytics.getRecommendations();

      expect(api.get).toHaveBeenCalledWith('/swarm/analytics/recommendations');
      expect(result).toEqual(mockRecommendations);
    });

    it('handles empty recommendations', async () => {
      api.get.mockResolvedValue({ data: [] });

      const result = await swarmAnalytics.getRecommendations();

      expect(result).toEqual([]);
    });
  });
});
