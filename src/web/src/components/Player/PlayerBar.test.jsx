import PlayerBar from './PlayerBar';
import React from 'react';
import { PlayerProvider, usePlayer } from './PlayerContext';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('../../lib/nowPlaying', () => ({
  clearNowPlaying: vi.fn(() => Promise.resolve()),
  setNowPlaying: vi.fn(() => Promise.resolve()),
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
      <PlayerBar />
    </>
  );
};

const renderPlayer = () =>
  render(
    <PlayerProvider>
      <TestHarness />
    </PlayerProvider>,
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
    const source = audio.querySelector('source');
    await waitFor(() =>
      expect(source.getAttribute('src')).toContain('/api/v0/streams/sha256%3Atest'),
    );

    fireEvent.click(screen.getByTestId('player-toggle-mute'));

    expect(audio.muted).toBe(true);
    expect(source.getAttribute('src')).toContain('/api/v0/streams/sha256%3Atest');
    expect(window.localStorage.getItem('slskdn.player.localMuted')).toBe('true');
  });

  it('restores the local mute preference for the PWA/browser session', () => {
    window.localStorage.setItem('slskdn.player.localMuted', 'true');

    renderPlayer();
    fireEvent.click(screen.getByText('Play fixture'));

    expect(document.querySelector('audio').muted).toBe(true);
  });
});
