import ArtistReleaseRadarPanel from './ArtistReleaseRadarPanel';
import {
  fetchArtistReleaseRadarNotifications,
  fetchArtistReleaseRadarSubscriptions,
  routeArtistReleaseRadarNotification,
  subscribeArtistReleaseRadar,
} from '../../lib/musicBrainz';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';

vi.mock('../../lib/musicBrainz', () => ({
  fetchArtistReleaseRadarNotifications: vi.fn(),
  fetchArtistReleaseRadarSubscriptions: vi.fn(),
  routeArtistReleaseRadarNotification: vi.fn(),
  subscribeArtistReleaseRadar: vi.fn(),
}));

describe('ArtistReleaseRadarPanel', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    fetchArtistReleaseRadarSubscriptions.mockResolvedValue({
      data: [
        {
          artistId: 'artist-1',
          artistName: 'Fixture Artist',
          enabled: true,
          id: 'sub-1',
          mutedReleaseGroupIds: ['rg-muted'],
          scope: 'trusted',
        },
      ],
    });
    fetchArtistReleaseRadarNotifications.mockResolvedValue({
      data: [
        {
          confidence: 0.91,
          id: 'notification-1',
          recordingId: 'recording-1',
          sourceRealm: 'trusted-realm',
          workRef: {
            artist: 'Fixture Artist',
            title: 'New Radar Track',
          },
        },
      ],
    });
    routeArtistReleaseRadarNotification.mockResolvedValue({
      data: {
        success: true,
      },
    });
    subscribeArtistReleaseRadar.mockResolvedValue({
      data: {},
    });
  });

  it('shows subscriptions and direct routing actions for radar hits', async () => {
    render(<ArtistReleaseRadarPanel />);

    expect(await screen.findByText('Fixture Artist')).toBeInTheDocument();
    expect(screen.getByText('Fixture Artist - New Radar Track')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Route New Radar Track radar hit' }),
    ).toBeInTheDocument();
  });

  it('saves artist radar subscriptions and routes notifications explicitly', async () => {
    render(<ArtistReleaseRadarPanel />);

    expect(await screen.findByText('Fixture Artist - New Radar Track')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Release radar artist MBID'), {
      target: { value: 'artist-2' },
    });
    fireEvent.change(screen.getByLabelText('Release radar artist name'), {
      target: { value: 'New Artist' },
    });
    fireEvent.change(screen.getByLabelText('Muted release group IDs'), {
      target: { value: 'rg-1, rg-2' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Watch Artist' }));

    await waitFor(() =>
      expect(subscribeArtistReleaseRadar).toHaveBeenCalledWith(
        expect.objectContaining({
          artistId: 'artist-2',
          artistName: 'New Artist',
          enabled: true,
          mutedReleaseGroupIds: ['rg-1', 'rg-2'],
          scope: 'trusted',
        }),
      ),
    );

    fireEvent.change(screen.getByLabelText('Release radar route target peers'), {
      target: { value: 'peer-a, peer-b' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Route New Radar Track radar hit' }),
    );

    await waitFor(() =>
      expect(routeArtistReleaseRadarNotification).toHaveBeenCalledWith(
        expect.objectContaining({
          notificationId: 'notification-1',
          targetPeerIds: ['peer-a', 'peer-b'],
        }),
      ),
    );
  });
});
