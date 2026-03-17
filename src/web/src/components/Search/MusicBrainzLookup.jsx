import * as discoveryGraph from '../../lib/discoveryGraph';
import * as searches from '../../lib/searches';
import { resolveTarget } from '../../lib/musicBrainz';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Form,
  Header,
  Input,
  List,
  Popup,
  Segment,
} from 'semantic-ui-react';
import DiscoveryGraphModal from './DiscoveryGraphModal';

const MusicBrainzLookup = ({ disabled }) => {
  const [releaseInput, setReleaseInput] = useState('');
  const [recordingInput, setRecordingInput] = useState('');
  const [discogsInput, setDiscogsInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [target, setTarget] = useState(null);
  const [graphLoading, setGraphLoading] = useState(false);
  const [graphOpen, setGraphOpen] = useState(false);
  const [graphData, setGraphData] = useState(null);
  const [graphRequest, setGraphRequest] = useState(null);

  const openDiscoveryGraph = async (request) => {
    setGraphLoading(true);
    setGraphOpen(true);
    setGraphRequest(request);

    try {
      const graph = await discoveryGraph.buildDiscoveryGraph(request);
      setGraphData(graph);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to build discovery graph',
      );
      setGraphOpen(false);
    } finally {
      setGraphLoading(false);
    }
  };

  const handleLookup = async () => {
    if (!releaseInput && !recordingInput && !discogsInput) {
      toast.error('Provide at least one MusicBrainz or Discogs identifier');
      return;
    }

    setLoading(true);

    try {
      const payload = {
        discogsReleaseId: discogsInput.trim() || undefined,
        recordingId: recordingInput.trim() || undefined,
        releaseId: releaseInput.trim() || undefined,
      };

      const response = await resolveTarget(payload);
      setTarget(response.data);

      toast.success(
        response.data.album
          ? `Loaded album ${response.data.album.title}`
          : `Loaded track ${response.data.track?.title}`,
      );
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to resolve target',
      );
    } finally {
      setLoading(false);
    }
  };

  const summary = useMemo(() => {
    if (!target) {
      return null;
    }

    if (target.album) {
      return (
        <List>
          <List.Item>
            <List.Header>Album</List.Header>
            <List.Description>
              {target.album.title} · {target.album.artist} ·{' '}
              {target.album.tracks?.length ?? 0} tracks
            </List.Description>
          </List.Item>
        </List>
      );
    }

    if (target.track) {
      return (
        <List>
          <List.Item>
            <List.Header>Track</List.Header>
            <List.Description>
              {target.track.title} · {target.track.artist} ·{' '}
              {target.track.duration
                ? `${(target.track.duration / 60_000).toFixed(2)} min`
                : 'unknown length'}
            </List.Description>
          </List.Item>
        </List>
      );
    }

    return null;
  }, [target]);

  const handleOpenGraph = async () => {
    if (target?.album?.musicBrainzReleaseId) {
      await openDiscoveryGraph({
        scope: 'album',
        releaseId: target.album.musicBrainzReleaseId,
        album: target.album.title,
        artist: target.album.artist,
      });
      return;
    }

    if (target?.track?.musicBrainzRecordingId) {
      await openDiscoveryGraph({
        scope: 'track',
        recordingId: target.track.musicBrainzRecordingId,
        title: target.track.title,
        artist: target.track.artist,
      });
    }
  };

  const handleGraphRecenter = async (nodeId) => {
    if (!nodeId) {
      return;
    }

    const [nodeType, rawId] = nodeId.split(':');
    if (nodeType === 'artist') {
      await openDiscoveryGraph({ scope: 'artist', artistId: rawId });
      return;
    }

    if (nodeType === 'album' || nodeType === 'release-group') {
      await openDiscoveryGraph({ scope: 'album', releaseId: rawId });
      return;
    }

    if (nodeType === 'track') {
      await openDiscoveryGraph({ scope: 'track', recordingId: rawId });
    }
  };

  const handleGraphCompare = async (nodeId, label) => {
    if (!graphRequest || !nodeId) {
      return;
    }

    await openDiscoveryGraph({
      ...graphRequest,
      compareLabel: label,
      compareNodeId: nodeId,
    });
  };

  const handleQueueNearby = async (graph) => {
    const queries = (graph?.nodes || [])
      .filter((node) => node.nodeType === 'track')
      .map((node) => node.label || '')
      .filter(Boolean)
      .slice(0, 8);

    if (queries.length === 0) {
      toast.error('No nearby track nodes were available to queue');
      return;
    }

    try {
      const count = await searches.createBatch({ queries });
      toast.success(`Started ${count} nearby graph searches`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to queue nearby searches',
      );
    }
  };

  return (
    <>
      <Segment
        className="musicbrainz-lookup-segment"
        raised
      >
        <Header as="h4">MusicBrainz / Discogs Lookup</Header>
        <Form>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="MusicBrainz Release ID"
              onChange={(event) => setReleaseInput(event.target.value)}
              placeholder="e.g. 1c3b3668-..."
              value={releaseInput}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="MusicBrainz Recording ID"
              onChange={(event) => setRecordingInput(event.target.value)}
              placeholder="e.g. 8af4c1b9-..."
              value={recordingInput}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Discogs Release/Master ID"
              onChange={(event) => setDiscogsInput(event.target.value)}
              placeholder="e.g. 123456"
              value={discogsInput}
            />
          </Form.Field>
          <Popup
            content="Resolve a canonical MusicBrainz or Discogs target when you already know an identifier and want exact metadata."
            position="top center"
            trigger={
              <Button
                disabled={disabled || loading}
                loading={loading}
                onClick={handleLookup}
                primary
              >
                Resolve target
              </Button>
            }
          />
          <Popup
            content="Open the Discovery Graph around the resolved MusicBrainz target to branch into nearby releases, tracks, and artists."
            position="top center"
            trigger={
              <Button
                disabled={!target}
                onClick={handleOpenGraph}
                style={{ marginLeft: '0.5em' }}
              >
                Graph
              </Button>
            }
          />
          <Popup
            content="Open the same canonical target as a wider atlas-style discovery surface for neighborhood browsing."
            position="top center"
            trigger={
              <Button
                disabled={!target}
                onClick={handleOpenGraph}
                style={{ marginLeft: '0.5em' }}
              >
                Atlas
              </Button>
            }
          />
        </Form>
        {summary}
      </Segment>
      <DiscoveryGraphModal
        graph={graphData}
        loading={graphLoading}
        onClose={() => setGraphOpen(false)}
        onCompare={handleGraphCompare}
        onQueueNearby={handleQueueNearby}
        onRecenter={handleGraphRecenter}
        onRestoreBranch={(branch) => branch?.request && openDiscoveryGraph(branch.request)}
        open={graphOpen}
      />
    </>
  );
};

export default MusicBrainzLookup;
