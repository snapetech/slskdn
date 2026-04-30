import PlayerBar from './PlayerBar';
import React from 'react';
import { PlayerProvider, usePlayer } from './PlayerContext';
import { MemoryRouter } from 'react-router-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import * as externalVisualizer from '../../lib/externalVisualizer';

vi.mock('../../lib/nowPlaying', () => ({
  clearNowPlaying: vi.fn(() => Promise.resolve()),
  setNowPlaying: vi.fn(() => Promise.resolve()),
}));

vi.mock('../../lib/collections', () => ({
  browseLibraryItems: vi.fn(({ path = '', query = '' } = {}) => {
    if (query) {
      return Promise.resolve({
        data: {
          breadcrumbs: [{ name: 'Library', path: '' }],
          directories: [],
          duplicatesRemoved: 2,
          files: [
            {
              bytes: 5242880,
              contentId: 'sha256:library',
              duplicateCount: 3,
              fileName: 'Library stream.ogg',
              mediaKind: 'Audio',
              path: 'Downloads/Library stream.ogg',
            },
          ],
          hasMore: false,
          totalDirectories: 0,
          totalFiles: 1,
        },
      });
    }

    if (path === 'Downloads') {
      return Promise.resolve({
        data: {
          breadcrumbs: [
            { name: 'Library', path: '' },
            { name: 'Downloads', path: 'Downloads' },
          ],
          directories: [],
          duplicatesRemoved: 0,
          files: [
            {
              bytes: 5242880,
              contentId: 'sha256:library',
              duplicateCount: 1,
              fileName: 'Library stream.ogg',
              mediaKind: 'Audio',
              path: 'Downloads/Library stream.ogg',
            },
          ],
          hasMore: false,
          totalDirectories: 0,
          totalFiles: 1,
        },
      });
    }

    return Promise.resolve({
      data: {
        breadcrumbs: [{ name: 'Library', path: '' }],
        directories: [
          {
            childDirectoryCount: 2,
            fileCount: 1,
            name: 'Downloads',
            path: 'Downloads',
          },
        ],
        duplicatesRemoved: 0,
        files: [],
        hasMore: false,
        totalDirectories: 1,
        totalFiles: 0,
      },
    });
  }),
  getCollectionItems: vi.fn(() =>
    Promise.resolve({
      data: [
        {
          contentId: 'sha256:collection',
          fileName: 'Collection stream.ogg',
          id: 'collection-item-1',
          mediaKind: 'Audio',
        },
      ],
    }),
  ),
  getCollections: vi.fn(() =>
    Promise.resolve({ data: [{ id: 'collection-1', title: 'Favorites' }] }),
  ),
  searchLibraryItems: vi.fn(() =>
    Promise.resolve({
      data: {
        items: [
          {
            contentId: 'sha256:library',
            fileName: 'Library stream.ogg',
            mediaKind: 'Audio',
            path: '/downloads/Library stream.ogg',
          },
        ],
      },
    }),
  ),
}));

vi.mock('../../lib/externalVisualizer', () => ({
  getExternalVisualizerStatus: vi.fn(() =>
    Promise.resolve({
      arguments: [],
      available: true,
      configured: true,
      enabled: true,
      name: 'MilkDrop3',
      path: '/opt/MilkDrop3/MilkDrop 3.exe',
      resolvedPath: '/opt/MilkDrop3/MilkDrop 3.exe',
      workingDirectory: '/opt/MilkDrop3',
    }),
  ),
  launchExternalVisualizer: vi.fn(() =>
    Promise.resolve({
      error: null,
      name: 'MilkDrop3',
      processId: 1234,
      started: true,
    }),
  ),
}));

vi.mock('../../lib/streaming', () => ({
  buildDirectStreamUrl: vi.fn((contentId) =>
    `/api/v0/streams/${encodeURIComponent(contentId)}`,
  ),
  buildTicketedStreamUrl: vi.fn((contentId, ticket) =>
    `/api/v0/streams/${encodeURIComponent(contentId)}?ticket=${ticket}`,
  ),
  createStreamTicket: vi.fn(() => Promise.resolve('ticket-1')),
}));

const TestHarness = () => {
  const { playItem } = usePlayer();

  return (
    <>
      <button
        onClick={() =>
          playItem({
            contentId: 'sha256:test',
            fileName: 'Local stream.ogg',
            title: 'Local stream',
          })
        }
        type="button"
      >
        Play fixture
      </button>
      <button
        onClick={() =>
          playItem({
            contentId: 'sha256:second',
            fileName: 'Second stream.ogg',
            title: 'Second stream',
          })
        }
        type="button"
      >
        Play second fixture
      </button>
      <PlayerBar />
    </>
  );
};

const renderPlayer = () =>
  render(
    <MemoryRouter>
      <PlayerProvider>
        <TestHarness />
      </PlayerProvider>
    </MemoryRouter>,
  );

describe('PlayerBar', () => {
  beforeEach(() => {
    window.localStorage.clear();
    HTMLMediaElement.prototype.load = vi.fn();
    HTMLMediaElement.prototype.play = vi.fn(() => Promise.resolve());
    HTMLMediaElement.prototype.pause = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('mutes local browser playback without clearing the stream source', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));

    const audio = document.querySelector('audio');
    await waitFor(() => {
      const src = audio.getAttribute('src') || '';
      expect(src).toContain(
        '/api/v0/streams/sha256%3Atest',
      );
    });

    fireEvent.click(screen.getByTestId('player-toggle-mute'));

    expect(audio.muted).toBe(true);
    expect(audio.getAttribute('src')).toContain(
      '/api/v0/streams/sha256%3Atest',
    );
    expect(window.localStorage.getItem('slskdn.player.localMuted')).toBe('true');
  });

  it('restores the local mute preference for the PWA/browser session', () => {
    window.localStorage.setItem('slskdn.player.localMuted', 'true');

    renderPlayer();
    fireEvent.click(screen.getByText('Play fixture'));

    expect(document.querySelector('audio').muted).toBe(true);
  });

  it('opens collection and local file browser modals before playback starts', async () => {
    renderPlayer();

    expect(
      screen.getByTestId('player-open-collections-browser'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('player-open-file-browser')).toBeInTheDocument();
    await screen.findByText('Pick a collection or local audio file');

    fireEvent.click(screen.getByTestId('player-open-collections-browser'));
    expect(
      screen.getByTestId('player-collection-browser-modal'),
    ).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('player-collection-row-collection-1'));
    expect(await screen.findByText('Collection stream.ogg')).toBeInTheDocument();

    fireEvent.click(screen.getAllByText('Close')[0]);
    fireEvent.click(screen.getByTestId('player-open-file-browser'));
    expect(screen.getByTestId('player-file-browser-modal')).toBeInTheDocument();
    expect(await screen.findByText('Downloads')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('player-file-folder-Downloads'));
    expect(await screen.findByText('Library stream.ogg')).toBeInTheDocument();
  });

  it('searches the local file browser as a deduplicated explorer', async () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-file-browser'));
    fireEvent.change(screen.getByTestId('player-file-browser-search').querySelector('input'), {
      target: { value: 'library' },
    });

    expect(await screen.findByText('Library stream.ogg')).toBeInTheDocument();
    expect(screen.getByText(/2 duplicates collapsed/u)).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('switches the visual tile from album art to the MilkDrop canvas', () => {
    renderPlayer();

    expect(screen.getByTestId('player-album-art')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-visual-tile'));

    expect(document.querySelector('.player-visualizer-canvas')).toBeInTheDocument();
    expect(screen.queryByTestId('player-album-art')).not.toBeInTheDocument();
  });

  it('does not repeat the currently playing track in the queue preview', () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));

    expect(screen.getByText('Local stream')).toBeInTheDocument();
    expect(document.querySelector('.player-queue')).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('Play second fixture'));

    expect(screen.getByText('Second stream')).toBeInTheDocument();
    expect(screen.getByText('Local stream')).toBeInTheDocument();
    expect(document.querySelector('.player-queue')?.textContent).not.toContain(
      'Second stream',
    );
  });

  it('makes ListenBrainz autosave explicit and clearable', () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-integrations'));
    const tokenInput = screen.getByLabelText('ListenBrainz user token');

    fireEvent.change(tokenInput, { target: { value: ' token-1 ' } });

    expect(screen.getByTestId('player-listenbrainz-save-state')).toHaveTextContent(
      'saved automatically',
    );
    expect(window.localStorage.getItem('slskdn.listenbrainz.token')).toBe('token-1');
    expect(screen.getByTestId('player-close-integrations')).toHaveTextContent('Done');

    fireEvent.click(screen.getByTestId('player-clear-listenbrainz-token'));

    expect(tokenInput).toHaveValue('');
    expect(window.localStorage.getItem('slskdn.listenbrainz.token')).toBeNull();
  });

  it('shows and launches the configured external visualizer', async () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-integrations'));

    expect(
      await screen.findByText('Ready to launch on the slskdN host.'),
    ).toBeInTheDocument();
    expect(screen.getByText('/opt/MilkDrop3/MilkDrop 3.exe')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-launch-external-visualizer'));

    await waitFor(() => {
      expect(externalVisualizer.launchExternalVisualizer).toHaveBeenCalled();
    });
    expect(await screen.findByText('MilkDrop3 launched.')).toBeInTheDocument();
  });
});
