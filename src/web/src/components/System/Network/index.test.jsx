// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import * as slskdnAPI from '../../../lib/slskdn';
import Network from '.';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

vi.mock('../../../lib/slskdn');
vi.mock('../../Shared', () => ({
  LoaderSegment: () => <div>Loading...</div>,
  ShrinkableButton: ({ children, ...props }) => (
    <button {...props}>{children}</button>
  ),
}));
vi.mock('react-toastify', () => ({
  toast: {
    error: vi.fn(),
    info: vi.fn(),
    success: vi.fn(),
  },
}));

describe('Network', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.clear();
    slskdnAPI.getSlskdnStats.mockResolvedValue({
      backfill: {
        completedToday: 0,
        discoveryRate: 0,
        isActive: false,
        pendingCount: 0,
      },
      capabilities: { features: [], version: 'slskdn' },
      dht: {
        dhtNodeCount: 0,
        isEnabled: true,
        isLanOnly: false,
        isDhtRunning: true,
      },
      hashDb: { currentSeqId: 0, totalEntries: 0 },
      mesh: {
        connectedPeerCount: 0,
        warnings: [],
      },
      swarmJobs: [],
    });
    slskdnAPI.getMeshPeers.mockResolvedValue([]);
    slskdnAPI.getDiscoveredPeers.mockResolvedValue([]);
  });

  it('shows the connectivity diagnostics warning when no peers are reachable', async () => {
    render(<Network theme="light" />);

    await waitFor(() => {
      expect(screen.getByText('Connectivity diagnostics')).toBeInTheDocument();
    });

    expect(
      screen.getByText(/configured Soulseek listen port is reachable/i),
    ).toBeInTheDocument();
  });

  it('shows the DHT exposure notice for first-run public DHT usage', async () => {
    render(<Network theme="light" />);

    await waitFor(() => {
      expect(screen.getByText('Public DHT exposure consent')).toBeInTheDocument();
      expect(screen.getByText('I understand')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('I understand'));

    await waitFor(() => {
      expect(
        screen.getByText('Public DHT exposure notice'),
      ).toBeInTheDocument();
    });

    expect(
      screen.getByText(/dht rendezvous is enabled/i),
    ).toBeInTheDocument();
  });

  it('does not show the DHT consent modal if already acknowledged', async () => {
    window.localStorage.setItem('slskdn:ui:dht-public-exposure:consent-v1', 'acknowledged');

    render(<Network theme="light" />);

    await waitFor(() => {
      expect(
        screen.getByText('Public DHT exposure notice'),
      ).toBeInTheDocument();
    });

    expect(screen.queryByText('Public DHT exposure consent')).not.toBeInTheDocument();
  });

  it('does not show the DHT exposure notice when DHT is LAN-only', async () => {
    slskdnAPI.getSlskdnStats.mockResolvedValueOnce({
      backfill: {
        completedToday: 0,
        discoveryRate: 0,
        isActive: false,
        pendingCount: 0,
      },
      capabilities: { features: [], version: 'slskdn' },
      dht: {
        dhtNodeCount: 3,
        isEnabled: true,
        isLanOnly: true,
        isDhtRunning: true,
      },
      hashDb: { currentSeqId: 0, totalEntries: 0 },
      mesh: {
        connectedPeerCount: 0,
        warnings: [],
      },
      swarmJobs: [],
    });

    render(<Network theme="light" />);

    await waitFor(() => {
      expect(screen.queryByText('Public DHT exposure notice')).not.toBeInTheDocument();
    });
  });

  it('renders inverted statistics in dark theme', async () => {
    const { container } = render(<Network theme="dark" />);

    await waitFor(() => {
      expect(screen.getByText('Mesh Sync Security')).toBeInTheDocument();
    });

    expect(container.querySelector('.ui.inverted.statistics')).not.toBeNull();
  });
});
