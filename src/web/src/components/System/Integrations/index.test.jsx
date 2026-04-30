import '@testing-library/jest-dom';
import * as lidarr from '../../../lib/lidarr';
import Integrations from './index';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../../../lib/lidarr', () => ({
  getStatus: vi.fn(),
  getWantedMissing: vi.fn(),
  importCompletedDirectory: vi.fn(),
  syncWanted: vi.fn(),
}));

describe('Integrations', () => {
  it('surfaces VPN and Lidarr settings without exposing secrets', () => {
    render(
      <Integrations
        options={{
          integration: {
            lidarr: {
              apiKey: 'secret-key',
              autoImportCompleted: true,
              enabled: true,
              importMode: 'move',
              syncWantedToWishlist: true,
              url: 'http://lidarr.local:8686',
            },
            vpn: {
              enabled: true,
              gluetun: {
                url: 'http://127.0.0.1:8000',
              },
              pollingInterval: 2500,
              portForwarding: true,
            },
          },
        }}
        state={{
          vpn: {
            forwardedPort: 50300,
            isConnected: true,
            isReady: true,
            publicIPAddress: '203.0.113.7',
          },
        }}
      />,
    );

    expect(screen.getByText('VPN')).toBeInTheDocument();
    expect(screen.getByText('203.0.113.7')).toBeInTheDocument();
    expect(screen.getByText('Lidarr')).toBeInTheDocument();
    expect(screen.getByText('http://lidarr.local:8686')).toBeInTheDocument();
    expect(screen.getByText('API Key Configured')).toBeInTheDocument();
    expect(screen.queryByText('secret-key')).not.toBeInTheDocument();
  });

  it('runs Lidarr admin actions', async () => {
    lidarr.getStatus.mockResolvedValue({
      appName: 'Lidarr',
      version: '2.0.0',
    });
    lidarr.syncWanted.mockResolvedValue({
      createdCount: 1,
      duplicateCount: 2,
      skippedCount: 3,
    });

    render(<Integrations />);

    fireEvent.click(screen.getByText('Check Status'));
    expect(await screen.findByText(/Lidarr responded: Lidarr 2.0.0/)).toBeInTheDocument();

    fireEvent.click(screen.getByText('Sync Wanted'));
    await waitFor(() => {
      expect(lidarr.syncWanted).toHaveBeenCalled();
    });
    expect(screen.getByText(/Wanted sync: 1 created/)).toBeInTheDocument();
  });
});
