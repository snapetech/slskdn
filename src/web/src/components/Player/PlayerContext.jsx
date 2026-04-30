import * as nowPlaying from '../../lib/nowPlaying';
import React, { createContext, useCallback, useContext, useState } from 'react';

export const PlayerContext = createContext({
  clear: () => {},
  current: null,
  followParty: () => {},
  followingParty: null,
  pause: () => {},
  playItem: () => {},
  queue: [],
  removeFromQueue: () => {},
  setAudioElement: () => {},
});

export const PlayerProvider = ({ children }) => {
  const [audioElement, setAudioElement] = useState(null);
  const [current, setCurrent] = useState(null);
  const [queue, setQueue] = useState([]);
  const [followingParty, setFollowingParty] = useState(null);

  const playItem = useCallback(
    async (item, options = {}) => {
      if (!item?.contentId) return;

      const playable = {
        album: item.album || item.collectionTitle || '',
        artist: item.artist || item.username || 'slskdN',
        contentId: item.contentId,
        fileName: item.fileName || item.title || item.contentId,
        positionSeconds: options.positionSeconds || 0,
        streamUrl: item.streamUrl || options.streamUrl || '',
        title: item.title || item.fileName || item.contentId,
      };

      setCurrent(playable);
      setQueue((existing) =>
        options.replaceQueue ? [playable] : [playable, ...existing],
      );

      if (playable.artist && playable.title) {
        await nowPlaying.setNowPlaying({
          album: playable.album,
          artist: playable.artist,
          title: playable.title,
        });
      }

      window.setTimeout(() => {
        if (audioElement) {
          audioElement.play().catch(() => {});
        }
      }, 0);
    },
    [audioElement],
  );

  const pause = useCallback(() => {
    if (audioElement) {
      audioElement.pause();
    }
  }, [audioElement]);

  const clear = useCallback(async () => {
    if (audioElement) {
      audioElement.pause();
      audioElement.removeAttribute('src');
      audioElement.load();
    }

    setCurrent(null);
    setQueue([]);
    await nowPlaying.clearNowPlaying();
  }, [audioElement]);

  const removeFromQueue = useCallback((contentId) => {
    setQueue((existing) =>
      existing.filter((item) => item.contentId !== contentId),
    );
  }, []);

  const followParty = useCallback((partyState) => {
    setFollowingParty(partyState);
  }, []);

  return (
    <PlayerContext.Provider
      value={{
        clear,
        current,
        followParty,
        followingParty,
        pause,
        playItem,
        queue,
        removeFromQueue,
        setAudioElement,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = () => useContext(PlayerContext);
