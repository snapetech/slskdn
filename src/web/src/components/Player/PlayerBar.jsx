import './Player.css';
import { urlBase } from '../../config';
import { usePlayer } from './PlayerContext';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Icon, Popup } from 'semantic-ui-react';

const streamUrl = (contentId) =>
  `${urlBase}/api/v0/streams/${encodeURIComponent(contentId)}`;

const PlayerBar = () => {
  const audioRef = useRef(null);
  const {
    clear,
    current,
    followingParty,
    pause,
    queue,
    removeFromQueue,
    setAudioElement,
  } = usePlayer();
  const [playing, setPlaying] = useState(false);

  useEffect(() => {
    setAudioElement(audioRef.current);
  }, [setAudioElement]);

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

  return (
    <div className="player-bar">
      <audio
        onLoadedMetadata={() => {
          if (audioRef.current && current?.positionSeconds > 0) {
            audioRef.current.currentTime = current.positionSeconds;
          }
        }}
        onPause={() => setPlaying(false)}
        onPlay={() => setPlaying(true)}
        ref={audioRef}
      >
        {source ? <source src={source} /> : null}
      </audio>
      <div className="player-track">
        <Icon name="music" />
        <div>
          <div className="player-title">
            {current?.title || 'Nothing playing'}
          </div>
          <div className="player-subtitle">
            {current?.artist || 'Select a playlist item to start playback'}
            {followingParty ? ` | Following ${followingParty.hostPeerId}` : ''}
          </div>
        </div>
      </div>
      <div className="player-controls">
        <Popup
          content={playing ? 'Pause the current stream.' : 'Resume the current stream.'}
          trigger={
            <Button
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
          content="Stop playback and clear your now-playing profile status."
          trigger={
            <Button
              disabled={!current}
              icon
              onClick={clear}
            >
              <Icon name="stop" />
            </Button>
          }
        />
      </div>
      <div className="player-queue">
        {queue.slice(0, 3).map((item) => (
          <button
            className="player-queue-item"
            key={item.contentId}
            onClick={() => removeFromQueue(item.contentId)}
            title="Remove this item from the visible queue."
            type="button"
          >
            {item.title}
          </button>
        ))}
      </div>
    </div>
  );
};

export default PlayerBar;
