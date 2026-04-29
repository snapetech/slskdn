import * as discoveryGraph from '../../lib/discoveryGraph';
import * as searches from '../../lib/searches';
import './Search.css';
import DiscoveryGraphAtlas from './DiscoveryGraphAtlas';
import React, { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Dropdown,
  Form,
  Header,
  Input,
  Label,
  List,
  Popup,
  Segment,
} from 'semantic-ui-react';

const scopeOptions = [
  { key: 'songid_run', text: 'Song / Unknown Seed', value: 'songid_run' },
  { key: 'track', text: 'Track', value: 'track' },
  { key: 'album', text: 'Album', value: 'album' },
  { key: 'artist', text: 'Artist', value: 'artist' },
];

const DiscoveryGraphAtlasPanel = ({ disabled, persistRoute = false }) => {
  const location = useLocation();
  const navigate = useNavigate();
  const [scope, setScope] = useState('songid_run');
  const [artist, setArtist] = useState('');
  const [album, setAlbum] = useState('');
  const [title, setTitle] = useState('');
  const [artistId, setArtistId] = useState('');
  const [releaseId, setReleaseId] = useState('');
  const [recordingId, setRecordingId] = useState('');
  const [graph, setGraph] = useState(null);
  const [loading, setLoading] = useState(false);
  const [maxDepth, setMaxDepth] = useState(2);
  const [minNodeWeight, setMinNodeWeight] = useState(0.2);
  const [savedBranches, setSavedBranches] = useState([]);

  const loadSavedBranches = () => {
    try {
      const raw = window.localStorage.getItem('slskdn.discoveryGraph.savedBranches');
      const parsed = raw ? JSON.parse(raw) : [];
      setSavedBranches(Array.isArray(parsed) ? parsed : []);
    } catch (error) {
      console.warn('Failed to load saved Discovery Graph branches', error);
      setSavedBranches([]);
    }
  };

  useEffect(() => {
    loadSavedBranches();
  }, []);

  useEffect(() => {
    if (!persistRoute) {
      return;
    }

    const request = discoveryGraph.fromQueryString(location.search);
    setScope(request.scope || 'songid_run');
    setArtist(request.artist || '');
    setAlbum(request.album || '');
    setTitle(request.title || '');
    setArtistId(request.artistId || '');
    setReleaseId(request.releaseId || '');
    setRecordingId(request.recordingId || '');

    const hasSeed =
      request.artist ||
      request.album ||
      request.title ||
      request.artistId ||
      request.releaseId ||
      request.recordingId ||
      request.songIdRunId;

    if (hasSeed) {
      openGraph(request);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [persistRoute]);

  const buildRequest = () => ({
    album: album.trim() || undefined,
    artist: artist.trim() || undefined,
    artistId: artistId.trim() || undefined,
    recordingId: recordingId.trim() || undefined,
    releaseId: releaseId.trim() || undefined,
    scope,
    title: title.trim() || undefined,
  });

  const openGraph = async (request) => {
    setLoading(true);

    try {
      const nextGraph = await discoveryGraph.buildDiscoveryGraph(request);
      setGraph(nextGraph);
      if (persistRoute) {
        const query = discoveryGraph.toQueryString(request);
        navigate({
          pathname: location.pathname,
          search: query ? `?${query}` : '',
        }, { replace: true });
      }
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to build discovery graph',
      );
    } finally {
      setLoading(false);
    }
  };

  const handleBuild = async () => {
    await openGraph(buildRequest());
  };

  const handleRecenter = async (nodeId) => {
    if (!nodeId) {
      return;
    }

    const [nodeType, rawId] = nodeId.split(':');
    if (nodeType === 'artist') {
      await openGraph({ scope: 'artist', artistId: rawId });
      return;
    }

    if (nodeType === 'album' || nodeType === 'release-group') {
      await openGraph({ scope: 'album', releaseId: rawId });
      return;
    }

    if (nodeType === 'track') {
      await openGraph({ scope: 'track', recordingId: rawId });
      return;
    }

    await handleBuild();
  };

  const handleQueueNearby = async () => {
    const queries = (graph?.nodes || [])
      .filter((node) => node.nodeType === 'track')
      .map((node) => node.label || '')
      .filter(Boolean)
      .slice(0, 10);

    if (queries.length === 0) {
      toast.error('No nearby track nodes were available to queue');
      return;
    }

    try {
      const count = await searches.createBatch({ queries });
      toast.success(`Started ${count} nearby atlas searches`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to queue nearby atlas searches',
      );
    }
  };

  const nodes = Array.isArray(graph?.nodes) ? graph.nodes : [];
  const nodeMap = nodes.reduce((accumulator, node) => {
    accumulator[node.nodeId] = node;
    return accumulator;
  }, {});
  const visibleNodeIds = new Set(
    nodes
      .filter(
        (node) =>
          (node.depth || 0) <= maxDepth && (node.weight || 0) >= minNodeWeight,
      )
      .map((node) => node.nodeId),
  );
  const visibleEdges = (graph?.edges || []).filter(
    (edge) =>
      visibleNodeIds.has(edge.sourceNodeId) &&
      visibleNodeIds.has(edge.targetNodeId),
  );
  const edgeTypeSummary = Array.from(
    visibleEdges.reduce((accumulator, edge) => {
      accumulator.set(edge.edgeType, (accumulator.get(edge.edgeType) || 0) + 1);
      return accumulator;
    }, new Map()),
  );

  return (
    <Segment
      className="discovery-graph-atlas-panel"
      raised
    >
      <Header as="h4">Discovery Graph Atlas</Header>
      <p style={{ marginTop: 0 }}>
        Persistent graph surface for wandering the neighborhood around a seed
        without opening a modal. Use manual seeds, canonical ids, or saved
        branches to keep exploration in the page flow.
      </p>
      <Form className="discovery-graph-seed-form">
        <Form.Field>
          <label>Seed Scope</label>
          <Dropdown
            disabled={disabled || loading}
            fluid
            onChange={(_event, { value }) => setScope(value)}
            options={scopeOptions}
            selection
            value={scope}
          />
        </Form.Field>
        <Form.Group widths="equal">
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Artist"
              onChange={(event) => setArtist(event.target.value)}
              placeholder="Artist name"
              value={artist}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Album"
              onChange={(event) => setAlbum(event.target.value)}
              placeholder="Album title"
              value={album}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Title"
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Track title or seed label"
              value={title}
            />
          </Form.Field>
        </Form.Group>
        <Form.Group widths="equal">
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Artist MBID"
              onChange={(event) => setArtistId(event.target.value)}
              placeholder="Optional artist id"
              value={artistId}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Release ID"
              onChange={(event) => setReleaseId(event.target.value)}
              placeholder="Optional release id"
              value={releaseId}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={disabled || loading}
              label="Recording ID"
              onChange={(event) => setRecordingId(event.target.value)}
              placeholder="Optional recording id"
              value={recordingId}
            />
          </Form.Field>
        </Form.Group>
        <Popup
          content="Build a persistent atlas in-page from the current seed fields instead of opening a temporary modal."
          position="top center"
          trigger={
            <Button
              disabled={disabled || loading}
              loading={loading}
              onClick={handleBuild}
              primary
            >
              Build Atlas
            </Button>
          }
        />
        <Popup
          content="Queue nearby track nodes from the current atlas into regular searches so exploration turns into acquisition work."
          position="top center"
          trigger={
            <Button
              disabled={!graph}
              onClick={handleQueueNearby}
              style={{ marginLeft: '0.5em' }}
            >
              Queue Nearby
            </Button>
          }
        />
      </Form>
      <div className="discovery-graph-controls">
        <label className="discovery-graph-range">
          <span>Depth {maxDepth}</span>
          <Input
            max={4}
            min={0}
            onChange={(_event, { value }) =>
              setMaxDepth(Number.parseInt(value || '0', 10) || 0)
            }
            step={1}
            type="range"
            value={maxDepth}
          />
        </label>
        <label className="discovery-graph-range">
          <span>Weight {Math.round((minNodeWeight || 0) * 100)}</span>
          <Input
            max={1}
            min={0}
            onChange={(_event, { value }) =>
              setMinNodeWeight(Number.parseFloat(value || '0') || 0)
            }
            step={0.05}
            type="range"
            value={minNodeWeight}
          />
        </label>
      </div>
      {savedBranches.length > 0 ? (
        <div style={{ marginTop: '1em' }}>
          <Header as="h5">Saved Branches</Header>
          <List horizontal relaxed>
            {savedBranches.slice(0, 6).map((branch) => (
              <List.Item key={branch.id}>
                <Popup
                  content="Restore this saved branch into the atlas panel."
                  position="top center"
                  trigger={
                    <Button
                      onClick={() => branch.request && openGraph(branch.request)}
                      size="mini"
                    >
                      {branch.title}
                    </Button>
                  }
                />
              </List.Item>
            ))}
          </List>
        </div>
      ) : null}
      {graph ? (
        <>
          <Header as="h5" style={{ marginBottom: '0.35em', marginTop: '1em' }}>
            {graph.title}
          </Header>
          <p style={{ marginTop: 0 }}>{graph.summary}</p>
          <div style={{ marginBottom: '0.75em' }}>
            {edgeTypeSummary.map(([edgeType, count]) => (
              <Label key={edgeType} size="tiny">
                {edgeType} {count}
              </Label>
            ))}
          </div>
          <DiscoveryGraphAtlas
            edgeTypes={[]}
            graph={graph}
            maxDepth={maxDepth}
            minNodeWeight={minNodeWeight}
            onNodeClick={handleRecenter}
          />
          <Header as="h5" style={{ marginBottom: '0.35em', marginTop: '1em' }}>
            Why These Nodes Are Near
          </Header>
          <List divided relaxed>
            {visibleEdges.slice(0, 12).map((edge) => (
              <List.Item
                key={`${edge.sourceNodeId}-${edge.targetNodeId}-${edge.edgeType}`}
              >
                <List.Content floated="right">
                  <Popup
                    content="Recenter the atlas on this destination node so you can keep walking the graph from there."
                    position="top center"
                    trigger={
                      <Button
                        onClick={() => handleRecenter(edge.targetNodeId)}
                        size="mini"
                      >
                        Recenter
                      </Button>
                    }
                  />
                </List.Content>
                <List.Content>
                  <List.Header>
                    {(nodeMap[edge.sourceNodeId]?.label || edge.sourceNodeId)} →{' '}
                    {(nodeMap[edge.targetNodeId]?.label || edge.targetNodeId)}
                  </List.Header>
                  <List.Description>
                    {edge.edgeType} · {edge.reason}
                    {edge.provenance ? ` · ${edge.provenance}` : ''}
                  </List.Description>
                  {edge.scoreComponents &&
                  Object.keys(edge.scoreComponents).length > 0 ? (
                    <List.Description style={{ marginTop: '0.35em' }}>
                      {Object.entries(edge.scoreComponents)
                        .map(
                          ([key, value]) =>
                            `${key} ${Math.round((value || 0) * 100)}`,
                        )
                        .join(' | ')}
                    </List.Description>
                  ) : null}
                  {Array.isArray(edge.evidence) && edge.evidence.length > 0 ? (
                    <List.Description style={{ marginTop: '0.35em' }}>
                      {edge.evidence.join(' | ')}
                    </List.Description>
                  ) : null}
                </List.Content>
              </List.Item>
            ))}
          </List>
        </>
      ) : (
        <Segment
          className="discovery-graph-empty-state"
          placeholder
          secondary
          style={{ marginTop: '1em' }}
        >
          Build an atlas from a seed above or restore a saved branch.
        </Segment>
      )}
    </Segment>
  );
};

export default DiscoveryGraphAtlasPanel;
