// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import SourceProviders from '.';
import * as sourceProvidersApi from '../../../lib/sourceProviders';
import { render, screen, waitFor } from '@testing-library/react';
import React from 'react';

vi.mock('../../../lib/sourceProviders', () => ({
  getSourceProviders: vi.fn(),
}));

describe('SourceProviders', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows active and disabled provider enablement', async () => {
    sourceProvidersApi.getSourceProviders.mockResolvedValue({
      acquisitionPlanningEnabled: true,
      profilePolicies: [
        {
          autoDownloadEnabled: false,
          notes: 'Prefer trusted mesh candidates, then fall back to public Soulseek compatibility.',
          profileId: 'mesh-preferred',
          profileName: 'Mesh Preferred',
          providerPriority: ['LocalLibrary', 'NativeMesh', 'MeshDht', 'Soulseek'],
        },
      ],
      providers: [
        {
          active: true,
          capabilities: ['search', 'download'],
          description: 'Public Soulseek search and peer transfer path.',
          domain: 'Music',
          id: 'Soulseek',
          name: 'Soulseek',
          networkPolicy: 'Rate-limited searches.',
          registered: true,
          requiresConfiguration: false,
          riskLevel: 'public-network',
          sortOrder: 10,
        },
        {
          active: false,
          capabilities: ['search', 'download', 'checksum'],
          description: 'Explicitly configured private torrent or magnet sources.',
          disabledReason: 'Disabled by default.',
          domain: 'Any',
          id: 'Torrent',
          name: 'Private Torrent',
          networkPolicy: 'High-risk provider.',
          registered: true,
          requiresConfiguration: true,
          riskLevel: 'high-risk',
          sortOrder: 90,
        },
      ],
    });

    render(<SourceProviders />);

    expect(await screen.findByText('Soulseek')).toBeInTheDocument();
    expect(screen.getByText('Private Torrent')).toBeInTheDocument();
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0);
    expect(screen.getByText('Disabled')).toBeInTheDocument();
    expect(screen.getByText('Needs Config')).toBeInTheDocument();
    expect(screen.getByText('Disabled by default.')).toBeInTheDocument();
    expect(screen.getByText('Profile Provider Priority')).toBeInTheDocument();
    expect(screen.getByText('Mesh Preferred')).toBeInTheDocument();
    expect(screen.getByText('2. NativeMesh')).toBeInTheDocument();
    expect(screen.getByText('Manual')).toBeInTheDocument();
  });

  it('keeps providers visible when acquisition planning is disabled', async () => {
    sourceProvidersApi.getSourceProviders.mockResolvedValue({
      acquisitionPlanningEnabled: false,
      providers: [
        {
          active: false,
          capabilities: ['search'],
          description: 'Already indexed or shared files on this slskdN node.',
          disabledReason: 'VirtualSoulfind v2 acquisition planning is disabled.',
          domain: 'Music',
          id: 'LocalLibrary',
          name: 'Local Library',
          networkPolicy: 'No peer traffic.',
          registered: true,
          requiresConfiguration: false,
          riskLevel: 'local',
          sortOrder: 0,
        },
      ],
    });

    render(<SourceProviders />);

    expect(await screen.findByText('Acquisition planning is disabled')).toBeInTheDocument();
    expect(screen.getByText('Local Library')).toBeInTheDocument();
    expect(
      screen.getByText('VirtualSoulfind v2 acquisition planning is disabled.'),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(sourceProvidersApi.getSourceProviders).toHaveBeenCalledTimes(1),
    );
  });
});
