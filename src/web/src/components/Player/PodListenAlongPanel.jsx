import { createListeningPartyHubConnection } from '../../lib/hubFactory';
import * as listeningParty from '../../lib/listeningParty';
import { usePlayer } from './PlayerContext';
import React, { useEffect, useRef, useState } from 'react';
import { Button, Checkbox, Icon, Label, List, Popup, Segment } from 'semantic-ui-react';

const applyPartyState = (state, player) => {
  if (!state) return;

  if (state.action === 'play' || state.action === 'seek') {
    const elapsed =
      state.action === 'play'
        ? Math.max(0, (Date.now() - state.serverTimeUnixMs) / 1000)
        : 0;
    player.playItem(
      {
        album: state.album,
        artist: state.artist || state.hostPeerId,
        contentId: state.contentId,
        streamUrl: state.streamUrl,
        title: state.title || state.contentId,
      },
      {
        positionSeconds: (state.positionSeconds || 0) + elapsed,
        replaceQueue: true,
      },
    );
  } else if (state.action === 'pause') {
    player.pause();
  } else if (state.action === 'stop') {
    player.clear();
  }
};

const PodListenAlongPanel = ({ channelId, podId, user }) => {
  const player = usePlayer();
  const [connected, setConnected] = useState(false);
  const [directory, setDirectory] = useState([]);
  const [following, setFollowing] = useState(false);
  const [globalRadio, setGlobalRadio] = useState(false);
  const [meshStreaming, setMeshStreaming] = useState(false);
  const [partyState, setPartyState] = useState(null);
  const followingRef = useRef(false);
  const playerRef = useRef(player);

  useEffect(() => {
    followingRef.current = following;
  }, [following]);

  useEffect(() => {
    playerRef.current = player;
  }, [player]);

  useEffect(() => {
    if (!podId || !channelId) return undefined;

    let disposed = false;
    const hub = createListeningPartyHubConnection();

    hub.on('partyState', (state) => {
      setPartyState(state);
      if (followingRef.current) {
        playerRef.current.followParty(state);
        applyPartyState(state, playerRef.current);
      }
    });
    hub.onreconnecting(() => setConnected(false));
    hub.onreconnected(() => setConnected(true));
    hub.onclose(() => setConnected(false));

    hub
      .start()
      .then(() => hub.invoke('JoinParty', podId, channelId))
      .then(() => {
        if (!disposed) setConnected(true);
      })
      .catch(() => setConnected(false));

    listeningParty
      .getPartyState(podId, channelId)
      .then((state) => {
        if (!disposed) setPartyState(state);
      })
      .catch(() => {});

    return () => {
      disposed = true;
      hub.invoke('LeaveParty', podId, channelId).catch(() => {});
      hub.stop().catch(() => {});
    };
  }, [channelId, podId]);

  const refreshDirectory = async () => {
    try {
      setDirectory(await listeningParty.getPartyDirectory());
    } catch {
      setDirectory([]);
    }
  };

  useEffect(() => {
    refreshDirectory();
    const id = window.setInterval(refreshDirectory, 30_000);
    return () => window.clearInterval(id);
  }, []);

  const publish = async (action) => {
    const current = player.current;
    if (action !== 'stop' && !current?.contentId) return;

    const state = await listeningParty.publishPartyState(podId, channelId, {
      action,
      album: current?.album || '',
      allowMeshStreaming: meshStreaming,
      artist: current?.artist || user || '',
      contentId: current?.contentId || '',
      hostPeerId: user || 'local-peer',
      listed: globalRadio,
      partyId: partyState?.partyId || '',
      positionSeconds: current?.positionSeconds || 0,
      title: current?.title || current?.fileName || '',
    });
    setPartyState(state);
    await refreshDirectory();
  };

  const joinListedParty = (party) => {
    const streamUrl = listeningParty.buildRadioStreamUrl(party);
    player.followParty(party);
    player.playItem(
      {
        album: party.album,
        artist: party.artist || party.hostPeerId,
        contentId: party.contentId,
        streamUrl,
        title: party.title || party.contentId,
      },
      {
        replaceQueue: true,
        streamUrl,
      },
    );
  };

  return (
    <Segment className="pod-listen-along">
      <div className="pod-listen-along-main">
        <div>
          <strong>Listen Along</strong>
          <div className="pod-listen-along-state">
            {partyState
              ? `${partyState.hostPeerId} ${partyState.action}: ${partyState.title || partyState.contentId}`
              : 'No active party'}
          </div>
        </div>
        <Label color={connected ? 'green' : 'grey'}>
          {connected ? 'Live' : 'Offline'}
        </Label>
      </div>
      <div className="pod-listen-along-toggles">
        <Popup
          content="List this party in the slskdN mesh radio directory so nearby mesh members can discover it."
          trigger={
            <Checkbox
              checked={globalRadio}
              label="List globally"
              onChange={(event, data) => setGlobalRadio(data.checked)}
              toggle
            />
          }
        />
        <Popup
          content="Allow listeners who join from the directory to stream this party's current track from this slskdN node."
          trigger={
            <Checkbox
              checked={meshStreaming}
              disabled={!globalRadio}
              label="Mesh streaming"
              onChange={(event, data) => setMeshStreaming(data.checked)}
              toggle
            />
          }
        />
      </div>
      <Button.Group size="small">
        <Popup
          content="Follow this pod's host playback using your own stream access."
          trigger={
            <Button
              active={following}
              icon
              onClick={() => {
                const next = !following;
                setFollowing(next);
                if (next && partyState) {
                  player.followParty(partyState);
                  applyPartyState(partyState, player);
                } else {
                  player.followParty(null);
                }
              }}
            >
              <Icon name={following ? 'volume up' : 'volume off'} />
            </Button>
          }
        />
        <Popup
          content="Publish your current local player track as the pod listen-along host."
          trigger={
            <Button
              disabled={!player.current}
              icon
              onClick={() => publish('play')}
            >
              <Icon name="bullhorn" />
            </Button>
          }
        />
        <Popup
          content="Stop hosting listen-along metadata for this pod."
          trigger={
            <Button
              icon
              onClick={() => publish('stop')}
            >
              <Icon name="stop" />
            </Button>
          }
        />
      </Button.Group>
      {directory.length > 0 && (
        <div className="pod-listen-along-directory">
          <strong>Listed radio</strong>
          <List divided relaxed>
            {directory.slice(0, 6).map((party) => (
              <List.Item key={party.partyId}>
                <List.Content floated="right">
                  <Popup
                    content="Join this listed radio party and stream from the host's integrated slskdN endpoint when available."
                    trigger={
                      <Button
                        disabled={!party.allowMeshStreaming}
                        icon
                        onClick={() => joinListedParty(party)}
                        size="mini"
                      >
                        <Icon name="play" />
                      </Button>
                    }
                  />
                </List.Content>
                <List.Content>
                  <List.Header>{party.title || party.contentId}</List.Header>
                  <List.Description>
                    {party.hostPeerId}
                    {party.allowMeshStreaming ? ' | streamable' : ' | metadata only'}
                  </List.Description>
                </List.Content>
              </List.Item>
            ))}
          </List>
        </div>
      )}
    </Segment>
  );
};

export default PodListenAlongPanel;
