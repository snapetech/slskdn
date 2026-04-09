// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import * as slskdnAPI from '../../../lib/slskdn';
import Network from '.';
import { render, screen, waitFor } from '@testing-library/react';
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

  it('renders inverted statistics in dark theme', async () => {
    const { container } = render(<Network theme="dark" />);

    await waitFor(() => {
      expect(screen.getByText('Mesh Sync Security')).toBeInTheDocument();
    });

    expect(container.querySelector('.ui.inverted.statistics')).not.toBeNull();
  });
});
