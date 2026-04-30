import '@testing-library/jest-dom';
import * as lidarr from '../../../lib/lidarr';
import * as optionsApi from '../../../lib/options';
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

vi.mock('../../../lib/options', () => ({
  applyOverlay: vi.fn(),
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

  it('shows media-server adapter cards and path diagnostics', () => {
    render(<Integrations />);

    expect(screen.getByText('Media Servers')).toBeInTheDocument();
    expect(screen.getByText('Plex')).toBeInTheDocument();
    expect(screen.getByText('Jellyfin / Emby')).toBeInTheDocument();
    expect(screen.getByText('Navidrome')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('slskdN local file path'), {
      target: { value: '/downloads/complete/Artist/Album/track.flac' },
    });
    fireEvent.change(screen.getByLabelText('Media server file path'), {
      target: { value: '/library/music/Artist/Album/track.flac' },
    });
    fireEvent.change(screen.getByLabelText('Remote path map from'), {
      target: { value: '/downloads/complete' },
    });
    fireEvent.change(screen.getByLabelText('Remote path map to'), {
      target: { value: '/library/music' },
    });

    expect(screen.getByText('Mapped')).toBeInTheDocument();
    expect(
      screen.getByText('Mapped path: /library/music/Artist/Album/track.flac'),
    ).toBeInTheDocument();
  });

  it('shows Servarr setup readiness without running actions', () => {
    render(
      <Integrations
        options={{
          integration: {
            lidarr: {
              apiKey: 'fixture-key',
              autoImportCompleted: true,
              enabled: true,
              importPathFrom: '/downloads',
              importPathTo: '/library',
              syncWantedToWishlist: true,
              url: 'http://example.invalid:8686',
            },
          },
        }}
      />,
    );

    expect(screen.getByText('Servarr Setup')).toBeInTheDocument();
    expect(screen.getByText('5/5 checks ready')).toBeInTheDocument();
    expect(screen.getByText('Base URL configured')).toBeInTheDocument();
    expect(screen.getByText('Wanted pull enabled')).toBeInTheDocument();
    expect(screen.queryByText('fixture-key')).not.toBeInTheDocument();
  });

  it('applies source-feed integration toggles without exposing secrets', async () => {
    optionsApi.applyOverlay.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            lastfm: {
              apiKey: '*****',
              enabled: false,
            },
            spotify: {
              clientId: '*****',
              clientSecret: '*****',
              enabled: false,
              maxItemsPerImport: 500,
              market: 'US',
              timeoutSeconds: 20,
            },
            youtube: {
              apiKey: '*****',
              enabled: false,
            },
          },
          remoteConfiguration: true,
        }}
      />,
    );

    expect(screen.getByText('Source Feed Imports')).toBeInTheDocument();
    expect(screen.getByText('Spotify Client ID Configured')).toBeInTheDocument();
    expect(screen.queryByText('*****')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Enable Spotify source-feed imports'));
    fireEvent.click(screen.getByLabelText('Enable YouTube playlist source-feed imports'));
    fireEvent.click(screen.getByLabelText('Enable Last.fm source-feed imports'));
    fireEvent.change(screen.getByLabelText('Spotify client ID'), {
      target: { value: 'spotify-client' },
    });
    fireEvent.change(screen.getByLabelText('YouTube Data API key'), {
      target: { value: 'youtube-key' },
    });
    fireEvent.change(screen.getByLabelText('Last.fm API key'), {
      target: { value: 'lastfm-key' },
    });
    fireEvent.click(screen.getByText('Apply Settings'));

    await waitFor(() => {
      expect(optionsApi.applyOverlay).toHaveBeenCalledWith({
        integration: {
          lastFm: {
            apiKey: 'lastfm-key',
            enabled: true,
          },
          spotify: {
            clientId: 'spotify-client',
            enabled: true,
            market: 'US',
            maxItemsPerImport: 500,
            redirectUri: '',
            timeoutSeconds: 20,
          },
          youTube: {
            apiKey: 'youtube-key',
            enabled: true,
          },
        },
      });
    });
    expect(
      screen.getByText('Source-feed integration settings applied for this running daemon.'),
    ).toBeInTheDocument();
  });
});
