import './Player.css';
import * as collectionsAPI from '../../lib/collections';
import { urlBase } from '../../config';
import { usePlayer } from './PlayerContext';
import Visualizer from './Visualizer';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button, Dropdown, Icon, Popup } from 'semantic-ui-react';

const streamUrl = (contentId) =>
  `${urlBase}/api/v0/streams/${encodeURIComponent(contentId)}`;

const localMuteStorageKey = 'slskdn.player.localMuted';
const collapsedStorageKey = 'slskdn.player.collapsed';
const visualizerStorageKey = 'slskdn.player.visualizerEnabled';

const readStoredBoolean = (key) => {
  if (typeof window === 'undefined') return false;
  return window.localStorage.getItem(key) === 'true';
};

const PlayerLauncher = ({ onPlayItem }) => {
  const navigate = useNavigate();
  const [collections, setCollections] = useState([]);
  const [items, setItems] = useState([]);
  const [query, setQuery] = useState('');
  const [itemsLoading, setItemsLoading] = useState(false);

  useEffect(() => {
    let canceled = false;
    collectionsAPI
      .getCollections()
      .then((response) => {
        if (!canceled) setCollections(response.data || []);
      })
      .catch(() => {
        if (!canceled) setCollections([]);
      });

    return () => {
      canceled = true;
    };
  }, []);

  useEffect(() => {
    if (query && query.length < 2) {
      setItems([]);
      return undefined;
    }

    let canceled = false;
    const timeoutId = window.setTimeout(() => {
      setItemsLoading(true);
      collectionsAPI
        .searchLibraryItems(query, 'Audio', 12)
        .then((response) => {
          if (!canceled) setItems(response.data?.items || []);
        })
        .catch(() => {
          if (!canceled) setItems([]);
        })
        .finally(() => {
          if (!canceled) setItemsLoading(false);
        });
    }, query ? 200 : 0);

    return () => {
      canceled = true;
      window.clearTimeout(timeoutId);
    };
  }, [query]);

  const collectionOptions = collections.map((collection) => ({
    key: collection.id,
    text: collection.title,
    value: collection.id,
  }));
  const itemOptions = items.map((item) => ({
    content: (
      <div>
        <strong>{item.fileName || item.contentId}</strong>
        <div className="player-picker-meta">{item.path}</div>
      </div>
    ),
    key: item.contentId,
    text: item.fileName || item.contentId,
    value: item.contentId,
  }));

  return (
    <div className="player-launcher">
      <Popup
        content="Open Collections to play or manage playlists and share lists."
        trigger={
          <Button
            as={Link}
            compact
            data-testid="player-open-collections"
            icon
            labelPosition="left"
            size="small"
            to="/collections"
          >
            <Icon name="list" />
            Collections
          </Button>
        }
      />
      <Dropdown
        className="player-picker"
        data-testid="player-collection-picker"
        disabled={collectionOptions.length === 0}
        onChange={(_, { value }) => {
          if (value) navigate('/collections');
        }}
        options={collectionOptions}
        placeholder="Collection list"
        search
        selection
      />
      <Dropdown
        className="player-picker player-file-picker"
        data-testid="player-file-picker"
        loading={itemsLoading}
        noResultsMessage={
          query.length < 2 ? 'Type two characters' : 'No audio found'
        }
        onChange={(_, { value }) => {
          const item = items.find((candidate) => candidate.contentId === value);
          if (item) onPlayItem(item);
        }}
        onSearchChange={(_, { searchQuery }) => setQuery(searchQuery || '')}
        options={itemOptions}
        placeholder="Shared/downloaded audio"
        search
        selection
      />
    </div>
  );
};

const PlayerBar = () => {
  const audioRef = useRef(null);
  const {
    clear,
    current,
    followingParty,
    next,
    pause,
    queue,
    previous,
    removeFromQueue,
    seekRelative,
    setAudioElement,
    playItem,
  } = usePlayer();
  const [localMuted, setLocalMuted] = useState(() =>
    readStoredBoolean(localMuteStorageKey),
  );
  const [collapsed, setCollapsed] = useState(() =>
    readStoredBoolean(collapsedStorageKey),
  );
  const [playing, setPlaying] = useState(false);
  const [visualizerMode, setVisualizerMode] = useState(() =>
    readStoredBoolean(visualizerStorageKey) ? 'inline' : 'off',
  );

  useEffect(() => {
    setAudioElement(audioRef.current);
  }, [setAudioElement]);

  useEffect(() => {
    if (!audioRef.current) return;
    audioRef.current.muted = localMuted;
    window.localStorage.setItem(localMuteStorageKey, localMuted ? 'true' : 'false');
  }, [localMuted]);

  useEffect(() => {
    window.localStorage.setItem(collapsedStorageKey, collapsed ? 'true' : 'false');
  }, [collapsed]);

  useEffect(() => {
    window.localStorage.setItem(
      visualizerStorageKey,
      visualizerMode !== 'off' ? 'true' : 'false',
    );
  }, [visualizerMode]);

  const toggleVisualizer = () => {
    setVisualizerMode((mode) => (mode === 'off' ? 'inline' : 'off'));
  };

  const source = useMemo(
    () =>
      current?.streamUrl ||
      (current?.contentId ? streamUrl(current.contentId) : ''),
    [current],
  );

  useEffect(() => {
    if (!audioRef.current || !source) return;
    audioRef.current.load();
    audioRef.current.play().catch(() => {});
  }, [source]);

  useEffect(() => {
    if (!('mediaSession' in navigator) || !window.MediaMetadata) {
      return undefined;
    }
    if (!current) {
      navigator.mediaSession.metadata = null;
      return undefined;
    }

    navigator.mediaSession.metadata = new window.MediaMetadata({
      album: current.album || '',
      artist: current.artist || '',
      title: current.title || current.fileName || current.contentId,
    });

    const handlers = {
      nexttrack: next,
      pause,
      play: () => audioRef.current?.play().catch(() => {}),
      previoustrack: previous,
      seekbackward: () => seekRelative(-15),
      seekforward: () => seekRelative(30),
    };

    Object.entries(handlers).forEach(([action, handler]) => {
      try {
        navigator.mediaSession.setActionHandler(action, handler);
      } catch {
        // Some browsers expose a partial Media Session implementation.
      }
    });

    return () => {
      Object.keys(handlers).forEach((action) => {
        try {
          navigator.mediaSession.setActionHandler(action, null);
        } catch {
          // Some browsers expose a partial Media Session implementation.
        }
      });
    };
  }, [current, next, pause, previous, seekRelative]);

  const audio = (
    <audio
        onLoadedMetadata={() => {
          if (audioRef.current && current?.positionSeconds > 0) {
            audioRef.current.currentTime = current.positionSeconds;
          }
        }}
        onPause={() => setPlaying(false)}
        onPlay={() => setPlaying(true)}
        playsInline
        preload="metadata"
        ref={audioRef}
      >
        {source ? <source src={source} /> : null}
      </audio>
  );

  if (collapsed) {
    return (
      <div className="player-bar player-bar-collapsed">
        {audio}
        <div className="player-track">
          <Icon name="music" />
          <div>
            <div className="player-title">
              {current?.title || 'Player'}
            </div>
            <div className="player-subtitle">
              {current?.artist || 'Ready'}
            </div>
          </div>
        </div>
        <div className="player-controls">
          <Popup
            content="Expand the player drawer."
            trigger={
              <Button
                aria-label="Expand player"
                data-testid="player-expand"
                icon
                onClick={() => setCollapsed(false)}
              >
                <Icon name="angle up" />
              </Button>
            }
          />
          <Popup
            content={playing ? 'Pause the current stream.' : 'Resume the current stream.'}
            trigger={
              <Button
                aria-label={playing ? 'Pause local playback' : 'Resume local playback'}
                data-testid="player-collapsed-toggle-playback"
                disabled={!current}
                icon
                onClick={() => {
                  if (!audioRef.current) return;
                  if (playing) {
                    pause();
                  } else {
                    audioRef.current.play().catch(() => {});
                  }
                }}
              >
                <Icon name={playing ? 'pause' : 'play'} />
              </Button>
            }
          />
          <Popup
            content={
              localMuted
                ? 'Unmute playback on this device without changing the stream.'
                : 'Mute playback on this device without changing the stream.'
            }
            trigger={
              <Button
                aria-label={localMuted ? 'Unmute local playback' : 'Mute local playback'}
                data-testid="player-collapsed-toggle-mute"
                disabled={!current}
                icon
                onClick={() => setLocalMuted((muted) => !muted)}
              >
                <Icon name={localMuted ? 'volume off' : 'volume up'} />
              </Button>
            }
          />
        </div>
      </div>
    );
  }

  return (
    <div className="player-bar">
      {audio}
      <div className="player-track">
        <Icon name="music" />
        <div>
          <div className="player-title">
            {current?.title || 'Nothing playing'}
          </div>
          <div className="player-subtitle">
            {current?.artist || 'Pick a collection or local audio file'}
            {followingParty ? ` | Following ${followingParty.hostPeerId}` : ''}
          </div>
        </div>
      </div>
      <div className="player-controls">
        <Popup
          content="Collapse the player into a small drawer bar above the footer."
          trigger={
            <Button
              aria-label="Collapse player"
              data-testid="player-collapse"
              icon
              onClick={() => setCollapsed(true)}
            >
              <Icon name="angle down" />
            </Button>
          }
        />
        <Popup
          content="Go to the previous queue item, or restart the current stream."
          trigger={
            <Button
              aria-label="Previous local track"
              data-testid="player-previous"
              disabled={!current}
              icon
              onClick={previous}
            >
              <Icon name="step backward" />
            </Button>
          }
        />
        <Popup
          content="Rewind local playback by 15 seconds."
          trigger={
            <Button
              aria-label="Rewind local playback"
              data-testid="player-rewind"
              disabled={!current}
              icon
              onClick={() => seekRelative(-15)}
            >
              <Icon name="backward" />
            </Button>
          }
        />
        <Popup
          content={playing ? 'Pause the current stream.' : 'Resume the current stream.'}
          trigger={
            <Button
              disabled={!current}
              aria-label={playing ? 'Pause local playback' : 'Resume local playback'}
              data-testid="player-toggle-playback"
              icon
              onClick={() => {
                if (!audioRef.current) return;
                if (playing) {
                  pause();
                } else {
                  audioRef.current.play().catch(() => {});
                }
              }}
            >
              <Icon name={playing ? 'pause' : 'play'} />
            </Button>
          }
        />
        <Popup
          content="Fast-forward local playback by 30 seconds."
          trigger={
            <Button
              aria-label="Fast-forward local playback"
              data-testid="player-fast-forward"
              disabled={!current}
              icon
              onClick={() => seekRelative(30)}
            >
              <Icon name="forward" />
            </Button>
          }
        />
        <Popup
          content="Play the next queue item."
          trigger={
            <Button
              aria-label="Next local track"
              data-testid="player-next"
              disabled={!current || queue.length < 2}
              icon
              onClick={next}
            >
              <Icon name="step forward" />
            </Button>
          }
        />
        <Popup
          content={
            localMuted
              ? 'Unmute playback on this device without changing the stream.'
              : 'Mute playback on this device without changing the stream.'
          }
          trigger={
            <Button
              aria-label={localMuted ? 'Unmute local playback' : 'Mute local playback'}
              data-testid="player-toggle-mute"
              disabled={!current}
              icon
              onClick={() => setLocalMuted((muted) => !muted)}
            >
              <Icon name={localMuted ? 'volume off' : 'volume up'} />
            </Button>
          }
        />
        <Popup
          content="Stop playback and clear your now-playing profile status."
          trigger={
            <Button
              aria-label="Stop local playback"
              data-testid="player-stop"
              disabled={!current}
              icon
              onClick={clear}
            >
              <Icon name="stop" />
            </Button>
          }
        />
        <Popup
          content={
            visualizerMode === 'off'
              ? 'Show the MilkDrop visualizer.'
              : 'Hide the MilkDrop visualizer.'
          }
          trigger={
            <Button
              aria-label={
                visualizerMode === 'off'
                  ? 'Show MilkDrop visualizer'
                  : 'Hide MilkDrop visualizer'
              }
              data-testid="player-toggle-visualizer"
              icon
              onClick={toggleVisualizer}
              primary={visualizerMode !== 'off'}
            >
              <Icon name="eye" />
            </Button>
          }
        />
      </div>
      <Visualizer
        audioElement={audioRef.current}
        mode={visualizerMode}
        onModeChange={setVisualizerMode}
      />
      <div className={current ? 'player-queue' : 'player-queue player-queue-empty'}>
        {current ? (
          queue.slice(0, 3).map((item) => (
            <button
              className="player-queue-item"
              key={item.contentId}
              onClick={() => removeFromQueue(item.contentId)}
              title="Remove this item from the visible queue."
              type="button"
            >
              {item.title}
            </button>
          ))
        ) : (
          <PlayerLauncher
            onPlayItem={(item) => playItem(item, { replaceQueue: true })}
          />
        )}
      </div>
    </div>
  );
};

export default PlayerBar;
