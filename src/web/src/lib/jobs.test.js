// <copyright file="jobs.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import * as jobs from './jobs';
import api from './api';

// Mock the api module
jest.mock('./api', () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

describe('jobs', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getJobs', () => {
    it('calls API with no parameters when options are empty', async () => {
      api.get.mockResolvedValue({ data: { jobs: [], total: 0 } });

      await jobs.getJobs();

      expect(api.get).toHaveBeenCalledWith('/api/jobs');
    });

    it('calls API with type filter', async () => {
      api.get.mockResolvedValue({ data: { jobs: [], total: 0 } });

      await jobs.getJobs({ type: 'discography' });

      expect(api.get).toHaveBeenCalledWith('/api/jobs?type=discography');
    });

    it('calls API with status filter', async () => {
      api.get.mockResolvedValue({ data: { jobs: [], total: 0 } });

      await jobs.getJobs({ status: 'running' });

      expect(api.get).toHaveBeenCalledWith('/api/jobs?status=running');
    });

    it('calls API with multiple filters', async () => {
      api.get.mockResolvedValue({ data: { jobs: [], total: 0 } });

      await jobs.getJobs({
        type: 'discography',
        status: 'running',
        limit: 20,
        offset: 10,
        sortBy: 'created_at',
        sortOrder: 'desc',
      });

      expect(api.get).toHaveBeenCalledWith(
        '/api/jobs?type=discography&status=running&limit=20&offset=10&sortBy=created_at&sortOrder=desc',
      );
    });

    it('returns jobs data from API response', async () => {
      const mockJobs = [
        {
          id: 'job-1',
          type: 'discography',
          status: 'running',
          created_at: '2026-01-26T10:00:00Z',
        },
      ];
      api.get.mockResolvedValue({
        data: { jobs: mockJobs, total: 1, limit: 20, offset: 0 },
      });

      const result = await jobs.getJobs();

      expect(result).toEqual({
        jobs: mockJobs,
        total: 1,
        limit: 20,
        offset: 0,
      });
    });

    it('handles undefined optional parameters', async () => {
      api.get.mockResolvedValue({ data: { jobs: [], total: 0 } });

      await jobs.getJobs({
        type: 'discography',
        status: undefined,
        limit: undefined,
      });

      expect(api.get).toHaveBeenCalledWith('/api/jobs?type=discography');
    });
  });

  describe('getJob', () => {
    it('calls API with encoded job ID', async () => {
      const mockJob = {
        id: 'job-123',
        type: 'discography',
        status: 'completed',
      };
      api.get.mockResolvedValue({ data: mockJob });

      await jobs.getJob('job-123');

      expect(api.get).toHaveBeenCalledWith('/api/jobs/job-123');
    });

    it('encodes special characters in job ID', async () => {
      api.get.mockResolvedValue({ data: {} });

      await jobs.getJob('job/with/slashes');

      expect(api.get).toHaveBeenCalledWith(
        '/api/jobs/job%2Fwith%2Fslashes',
      );
    });

    it('returns job data from API response', async () => {
      const mockJob = {
        id: 'job-1',
        type: 'discography',
        status: 'running',
        progress: {
          releases_total: 10,
          releases_done: 5,
          releases_failed: 0,
        },
      };
      api.get.mockResolvedValue({ data: mockJob });

      const result = await jobs.getJob('job-1');

      expect(result).toEqual(mockJob);
    });
  });

  describe('getActiveSwarmJobs', () => {
    it('returns empty array when API returns empty jobs array', async () => {
      api.get.mockResolvedValue({ data: { jobs: [] } });

      const result = await jobs.getActiveSwarmJobs();

      expect(result).toEqual([]);
      expect(api.get).toHaveBeenCalledWith('/multisource/jobs');
    });

    it('returns jobs array from API response', async () => {
      const mockJobs = [
        {
          jobId: 'swarm-1',
          filename: '/path/to/file.mp3',
          activeSources: 3,
          downloadedBytes: 1024,
          totalBytes: 4096,
          progressPercent: 25,
        },
      ];
      api.get.mockResolvedValue({ data: { jobs: mockJobs } });

      const result = await jobs.getActiveSwarmJobs();

      expect(result).toEqual(mockJobs);
    });

    it('returns empty array when API response is not an array', async () => {
      api.get.mockResolvedValue({ data: { jobs: null } });

      const result = await jobs.getActiveSwarmJobs();

      expect(result).toEqual([]);
    });

    it('returns empty array when API response has no jobs property', async () => {
      api.get.mockResolvedValue({ data: {} });

      const result = await jobs.getActiveSwarmJobs();

      expect(result).toEqual([]);
    });

    it('handles API errors gracefully', async () => {
      api.get.mockRejectedValue(new Error('Network error'));

      const result = await jobs.getActiveSwarmJobs();

      expect(result).toEqual([]);
    });

    it('logs errors to console', async () => {
      const consoleSpy = jest.spyOn(console, 'debug').mockImplementation();
      api.get.mockRejectedValue(new Error('Network error'));

      await jobs.getActiveSwarmJobs();

      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to fetch swarm jobs:',
        expect.any(Error),
      );

      consoleSpy.mockRestore();
    });
  });

  describe('getSwarmJobStatus', () => {
    it('calls API with encoded job ID', async () => {
      const mockStatus = {
        jobId: 'swarm-1',
        state: 'running',
        percentComplete: 50,
      };
      api.get.mockResolvedValue({ data: mockStatus });

      await jobs.getSwarmJobStatus('swarm-1');

      expect(api.get).toHaveBeenCalledWith('/multisource/jobs/swarm-1');
    });

    it('returns job status from API response', async () => {
      const mockStatus = {
        jobId: 'swarm-1',
        state: 'running',
        totalChunks: 100,
        completedChunks: 50,
        percentComplete: 50,
        activeWorkers: 3,
        chunksPerSecond: 10.5,
      };
      api.get.mockResolvedValue({ data: mockStatus });

      const result = await jobs.getSwarmJobStatus('swarm-1');

      expect(result).toEqual(mockStatus);
    });

    it('returns null when job is not found (404)', async () => {
      const error = new Error('Not found');
      error.response = { status: 404 };
      api.get.mockRejectedValue(error);

      const result = await jobs.getSwarmJobStatus('nonexistent');

      expect(result).toBeNull();
    });

    it('throws error for non-404 errors', async () => {
      const error = new Error('Server error');
      error.response = { status: 500 };
      api.get.mockRejectedValue(error);

      await expect(jobs.getSwarmJobStatus('swarm-1')).rejects.toThrow(
        'Server error',
      );
    });

    it('handles errors without response property', async () => {
      const error = new Error('Network error');
      api.get.mockRejectedValue(error);

      await expect(jobs.getSwarmJobStatus('swarm-1')).rejects.toThrow(
        'Network error',
      );
    });
  });
});
