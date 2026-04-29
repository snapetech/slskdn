import '@testing-library/jest-dom';
import Footer from './Footer';
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

const { getSlskdnStats, getStats, getSpeeds, isLoggedIn } = vi.hoisted(() => ({
  getSlskdnStats: vi.fn(),
  getSpeeds: vi.fn(),
  getStats: vi.fn(),
  isLoggedIn: vi.fn(),
}));

vi.mock('../../lib/mesh', () => ({
  getStats,
}));

vi.mock('../../lib/session', () => ({
  isLoggedIn,
}));

vi.mock('../../lib/slskdn', () => ({
  getSlskdnStats,
}));

vi.mock('../../lib/transfers', () => ({
  getSpeeds,
}));

describe('Footer', () => {
  beforeEach(() => {
    isLoggedIn.mockReturnValue(true);
    getStats.mockResolvedValue({
      dht: 12,
      natType: 'FullCone',
      overlay: 3,
    });
    getSlskdnStats.mockResolvedValue({
      backfill: { isActive: true },
      dht: { discoveredPeerCount: 23, dhtNodeCount: 12 },
      hashDb: { currentSeqId: 14638, totalEntries: 7_300 },
      mesh: { connectedPeerCount: 6, isSyncing: true },
      swarmJobs: [{ id: 'job-1' }],
    });
    getSpeeds.mockResolvedValue({
      mesh: 2_048,
      soulseek: 4_096,
      total: 6_144,
    });
    localStorage.setItem('slskdn-karma', '2');
  });

  afterEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('renders slskdN network stats in the footer', async () => {
    render(<Footer />);

    await waitFor(() => {
      expect(screen.getByText('23 dht')).toBeInTheDocument();
    });

    expect(screen.getByText('6 mesh')).toBeInTheDocument();
    expect(screen.getByText('7.3K hashes')).toBeInTheDocument();
    expect(screen.getByText('seq:14638')).toBeInTheDocument();
    expect(screen.getByText('1 swarm')).toBeInTheDocument();
    expect(screen.getByText('backfill')).toBeInTheDocument();
    expect(screen.getByText('+2')).toBeInTheDocument();
  });
});
