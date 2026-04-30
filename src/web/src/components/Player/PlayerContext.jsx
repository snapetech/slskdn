import * as nowPlaying from '../../lib/nowPlaying';
import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
} from 'react';

export const PlayerContext = createContext({
  clear: () => {},
  current: null,
  followParty: () => {},
  followingParty: null,
  next: () => {},
  pause: () => {},
  playItem: () => {},
  previous: () => {},
  queue: [],
  removeFromQueue: () => {},
  seekRelative: () => {},
  setAudioElement: () => {},
});

export const PlayerProvider = ({ children }) => {
  const [audioElement, setAudioElement] = useState(null);
  const [current, setCurrent] = useState(null);
  const [history, setHistory] = useState([]);
  const [queue, setQueue] = useState([]);
  const [followingParty, setFollowingParty] = useState(null);

  useEffect(() => {
    if (!current?.artist || !current?.title) return;
    nowPlaying.setNowPlaying({
      album: current.album,
      artist: current.artist,
      title: current.title,
    });
  }, [current]);

  const playItem = useCallback(
    (item, options = {}) => {
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

      setHistory((existing) => (current ? [current, ...existing].slice(0, 25) : existing));
      setCurrent(playable);
      setQueue((existing) =>
        options.replaceQueue ? [playable] : [playable, ...existing],
      );

      window.setTimeout(() => {
        if (audioElement) {
          audioElement.play().catch(() => {});
        }
      }, 0);
    },
    [audioElement, current],
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
    setHistory([]);
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

  const seekRelative = useCallback(
    (seconds) => {
      if (!audioElement) return;
      const duration = Number.isFinite(audioElement.duration)
        ? audioElement.duration
        : Number.MAX_SAFE_INTEGER;
      audioElement.currentTime = Math.max(
        0,
        Math.min(duration, audioElement.currentTime + seconds),
      );
    },
    [audioElement],
  );

  const next = useCallback(() => {
    setQueue((existing) => {
      if (existing.length < 2) return existing;
      const [, nextItem, ...remaining] = existing;
      setHistory((previousHistory) =>
        current ? [current, ...previousHistory].slice(0, 25) : previousHistory,
      );
      setCurrent(nextItem);
      return [nextItem, ...remaining];
    });
  }, [current]);

  const previous = useCallback(() => {
    if (history.length === 0) {
      if (audioElement) {
        audioElement.currentTime = 0;
      }
      return;
    }

    const [previousItem, ...remainingHistory] = history;
    setHistory(remainingHistory);
    setCurrent(previousItem);
    setQueue((existing) => [previousItem, ...existing]);
  }, [audioElement, history]);

  return (
    <PlayerContext.Provider
      value={{
        clear,
        current,
        followParty,
        followingParty,
        next,
        pause,
        playItem,
        previous,
        queue,
        removeFromQueue,
        seekRelative,
        setAudioElement,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = () => useContext(PlayerContext);
