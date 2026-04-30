import '@testing-library/jest-dom';
import * as lidarr from '../../../lib/lidarr';
import * as optionsApi from '../../../lib/options';
import Integrations from './index';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../../lib/lidarr', () => ({
  getStatus: vi.fn(),
  getWantedMissing: vi.fn(),
  importCompletedDirectory: vi.fn(),
  syncWanted: vi.fn(),
}));

vi.mock('../../../lib/options', () => ({
  applyOverlay: vi.fn(),
  getYaml: vi.fn(),
  updateYaml: vi.fn(),
}));

describe('Integrations', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

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
    fireEvent.click(screen.getAllByText('Apply Runtime')[1]);

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

  it('persists source-feed settings to YAML from the web UI', async () => {
    optionsApi.getYaml.mockResolvedValue('integrations: {}\n');
    optionsApi.updateYaml.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            lastfm: {},
            spotify: {},
            youtube: {},
          },
          remoteConfiguration: true,
        }}
      />,
    );

    fireEvent.click(screen.getByLabelText('Enable YouTube playlist source-feed imports'));
    fireEvent.change(screen.getByLabelText('YouTube Data API key'), {
      target: { value: 'youtube-key' },
    });
    fireEvent.click(screen.getAllByText('Save YAML')[1]);

    await waitFor(() => {
      expect(optionsApi.updateYaml).toHaveBeenCalled();
    });
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain('youtube');
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain('api_key: youtube-key');
    expect(
      screen.getByText('Source-feed integration settings saved to YAML.'),
    ).toBeInTheDocument();
  });

  it('applies notification integration settings without exposing secrets', async () => {
    optionsApi.applyOverlay.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            ntfy: {
              enabled: false,
              notificationPrefix: 'slskd',
            },
            pushbullet: {
              accessToken: '*****',
              cooldownTime: 900000,
              enabled: false,
              notificationPrefix: 'From slskd:',
              retryAttempts: 3,
            },
            pushover: {
              token: '*****',
              userKey: '*****',
            },
          },
          remoteConfiguration: true,
        }}
      />,
    );

    expect(screen.getByText('Notifications')).toBeInTheDocument();
    expect(screen.getByText('Pushbullet Token Configured')).toBeInTheDocument();
    expect(screen.getByText('Pushover Secrets Configured')).toBeInTheDocument();
    expect(screen.queryByText('*****')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Enable Pushbullet notifications'));
    fireEvent.click(screen.getByLabelText('Enable Ntfy notifications'));
    fireEvent.change(screen.getByLabelText('Ntfy topic URL'), {
      target: { value: 'https://ntfy.sh/slskdn' },
    });
    fireEvent.click(screen.getAllByText('Apply Runtime')[0]);

    await waitFor(() => {
      expect(optionsApi.applyOverlay).toHaveBeenCalledWith({
        integration: {
          ntfy: {
            enabled: true,
            notificationPrefix: 'slskd',
            notifyOnPrivateMessage: true,
            notifyOnRoomMention: true,
            url: 'https://ntfy.sh/slskdn',
          },
          pushbullet: {
            cooldownTime: 900000,
            enabled: true,
            notificationPrefix: 'From slskd:',
            notifyOnPrivateMessage: true,
            notifyOnRoomMention: true,
            retryAttempts: 3,
          },
          pushover: {
            enabled: false,
            notificationPrefix: 'slskd',
            notifyOnPrivateMessage: true,
            notifyOnRoomMention: true,
          },
        },
      });
    });
    expect(
      screen.getByText(
        'Notification integration settings applied for this running daemon.',
      ),
    ).toBeInTheDocument();
  });

  it('persists notification settings to YAML from the web UI', async () => {
    optionsApi.getYaml.mockResolvedValue('integrations: {}\n');
    optionsApi.updateYaml.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            ntfy: {},
            pushbullet: {},
            pushover: {},
          },
          remoteConfiguration: true,
        }}
      />,
    );

    fireEvent.click(screen.getByLabelText('Enable Pushover notifications'));
    fireEvent.change(screen.getByLabelText('Pushover user key'), {
      target: { value: 'pushover-user' },
    });
    fireEvent.change(screen.getByLabelText('Pushover API token'), {
      target: { value: 'pushover-token' },
    });
    fireEvent.click(screen.getAllByText('Save YAML')[0]);

    await waitFor(() => {
      expect(optionsApi.updateYaml).toHaveBeenCalled();
    });
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain('pushover');
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain(
      'user_key: pushover-user',
    );
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain(
      'token: pushover-token',
    );
    expect(
      screen.getByText('Notification integration settings saved to YAML.'),
    ).toBeInTheDocument();
  });

  it('applies FTP integration settings without exposing secrets', async () => {
    optionsApi.applyOverlay.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            ftp: {
              address: 'ftp.old.invalid',
              enabled: false,
              password: '*****',
              port: 21,
              remotePath: '/',
            },
          },
          remoteConfiguration: true,
        }}
      />,
    );

    expect(screen.getByText('FTP Uploads')).toBeInTheDocument();
    expect(screen.getByText('Password Configured')).toBeInTheDocument();
    expect(screen.queryByText('*****')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Enable FTP completed-download uploads'));
    fireEvent.change(screen.getByLabelText('FTP server address'), {
      target: { value: 'ftp.example.net' },
    });
    fireEvent.change(screen.getByLabelText('FTP username'), {
      target: { value: 'slskdn' },
    });
    fireEvent.change(screen.getByLabelText('FTP password'), {
      target: { value: 'ftp-secret' },
    });
    fireEvent.change(screen.getByLabelText('FTP remote upload path'), {
      target: { value: '/incoming' },
    });
    fireEvent.click(screen.getAllByText('Apply Runtime')[2]);

    await waitFor(() => {
      expect(optionsApi.applyOverlay).toHaveBeenCalledWith({
        integration: {
          ftp: {
            address: 'ftp.example.net',
            connectionTimeout: 5000,
            enabled: true,
            encryptionMode: 'auto',
            ignoreCertificateErrors: false,
            overwriteExisting: true,
            password: 'ftp-secret',
            port: 21,
            remotePath: '/incoming',
            retryAttempts: 3,
            username: 'slskdn',
          },
        },
      });
    });
    expect(
      screen.getByText('FTP integration settings applied for this running daemon.'),
    ).toBeInTheDocument();
  });

  it('persists FTP settings to YAML from the web UI', async () => {
    optionsApi.getYaml.mockResolvedValue('integrations: {}\n');
    optionsApi.updateYaml.mockResolvedValue({});

    render(
      <Integrations
        options={{
          integration: {
            ftp: {},
          },
          remoteConfiguration: true,
        }}
      />,
    );

    fireEvent.click(screen.getByLabelText('Enable FTP completed-download uploads'));
    fireEvent.change(screen.getByLabelText('FTP server address'), {
      target: { value: 'ftp.example.net' },
    });
    fireEvent.change(screen.getByLabelText('FTP password'), {
      target: { value: 'ftp-secret' },
    });
    fireEvent.click(screen.getAllByText('Save YAML')[2]);

    await waitFor(() => {
      expect(optionsApi.updateYaml).toHaveBeenCalled();
    });
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain('ftp');
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain(
      'address: ftp.example.net',
    );
    expect(optionsApi.updateYaml.mock.calls[0][0].yaml).toContain(
      'password: ftp-secret',
    );
    expect(
      screen.getByText('FTP integration settings saved to YAML.'),
    ).toBeInTheDocument();
  });
});
