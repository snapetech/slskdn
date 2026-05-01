import * as discoveryGraph from '../../lib/discoveryGraph';
import * as jobs from '../../lib/jobs';
import * as musicBrainz from '../../lib/musicBrainz';
import * as searches from '../../lib/searches';
import * as songId from '../../lib/songid';
import DiscoveryGraphCanvas from './DiscoveryGraphCanvas';
import DiscoveryGraphModal from './DiscoveryGraphModal';
import React, { useEffect, useRef, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Divider,
  Form,
  Header,
  Input,
  Label,
  List,
  Popup,
  Progress,
  Grid,
  Segment,
} from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const getSyntheticVerdictColor = (verdict) => {
  switch (verdict) {
    case 'strong_suspicion':
      return 'orange';
    case 'moderate_suspicion':
      return 'yellow';
    case 'mixed_or_inconclusive':
      return 'grey';
    case 'low_signal':
      return 'green';
    default:
      return undefined;
  }
};

const getIdentityVerdictColor = (verdict) => {
  switch (verdict) {
    case 'recognized_cataloged_track':
      return 'green';
    case 'candidate_match_found':
      return 'blue';
    case 'likely_ai_or_channel_original':
      return 'orange';
    case 'needs_manual_review':
      return 'yellow';
    default:
      return undefined;
  }
};

const formatPercent = (value) => Math.round((value || 0) * 100);

const getStatusColor = (status) => {
  switch (status) {
    case 'completed':
      return 'green';
    case 'failed':
      return 'red';
    case 'running':
      return 'blue';
    case 'queued':
      return 'grey';
    default:
      return undefined;
  }
};

const getRunTitle = (item) =>
  item?.metadata?.title ||
  item?.query ||
  item?.summary ||
  item?.sourceType ||
  'SongID run';

const getTrackKey = (candidate) =>
  `${(candidate?.artist || '').trim().toLowerCase()}::${(candidate?.title || '').trim().toLowerCase()}`;

const getDedupedTracks = (tracks) => {
  if (!Array.isArray(tracks)) {
    return [];
  }

  const map = new Map();
  tracks.forEach((candidate) => {
    const key = getTrackKey(candidate);
    const current = map.get(key);
    if (!current || (candidate.actionScore || 0) > (current.actionScore || 0)) {
      map.set(key, {
        ...candidate,
        duplicateCount: (current?.duplicateCount || 0) + 1,
      });
    } else {
      current.duplicateCount = (current.duplicateCount || 1) + 1;
    }
  });

  return Array.from(map.values()).sort(
    (left, right) => (right.actionScore || 0) - (left.actionScore || 0),
  );
};

const getOptionKey = (option) => [
  option?.actionKind || '',
  option?.scope || '',
  option?.mode || '',
  option?.targetId || '',
  option?.searchText || '',
  Array.isArray(option?.searchTexts) ? option.searchTexts.join('|') : '',
].join('::');

const getDedupedOptions = (options) => {
  if (!Array.isArray(options)) {
    return [];
  }

  const map = new Map();
  options.forEach((option) => {
    const key = getOptionKey(option);
    const current = map.get(key);
    if (!current || (option.overallScore || 0) > (current.overallScore || 0)) {
      map.set(key, {
        ...option,
        duplicateCount: (current?.duplicateCount || 0) + 1,
      });
    } else {
      current.duplicateCount = (current.duplicateCount || 1) + 1;
    }
  });

  return Array.from(map.values()).sort(
    (left, right) => (right.overallScore || 0) - (left.overallScore || 0),
  );
};

const getPlanKey = (plan) => [
  plan?.kind || '',
  plan?.targetId || '',
  plan?.profile || '',
  plan?.searchText || '',
  plan?.title || '',
].join('::');

const getDedupedPlans = (plans) => {
  if (!Array.isArray(plans)) {
    return [];
  }

  const map = new Map();
  plans.forEach((plan) => {
    const key = getPlanKey(plan);
    const current = map.get(key);
    if (!current || (plan.actionScore || 0) > (current.actionScore || 0)) {
      map.set(key, {
        ...plan,
        duplicateCount: (current?.duplicateCount || 0) + 1,
      });
    } else {
      current.duplicateCount = (current.duplicateCount || 1) + 1;
    }
  });

  return Array.from(map.values()).sort(
    (left, right) => (right.actionScore || 0) - (left.actionScore || 0),
  );
};

const getUniqueTopActions = (actions, limit) => {
  const seen = new Set();
  return actions.filter((action) => {
    const key = (action.label || '').toLowerCase();
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  }).slice(0, limit);
};

const hasScorecardSignal = (scorecard) => scorecard && [
  'clipCount',
  'acoustIdHitCount',
  'rawAcoustIdHitCount',
  'songRecHitCount',
  'songRecDistinctMatchCount',
  'transcriptCount',
  'ocrCount',
  'commentFindingCount',
  'timestampHintCount',
  'chapterHintCount',
  'playlistRequestCount',
  'aiCommentMentionCount',
  'panakoHitCount',
  'audfprintHitCount',
  'corpusMatchCount',
  'provenanceSignalCount',
  'aiArtifactClipCount',
].some((key) => (scorecard[key] || 0) > 0);

const detailStyle = {
  borderTop: '1px solid rgba(34, 36, 38, 0.15)',
  marginTop: '1em',
  paddingTop: '0.75em',
};

const detailSummaryStyle = {
  cursor: 'pointer',
  fontWeight: 600,
};

const SongIDPanel = ({ disabled }) => {
  const [source, setSource] = useState('');
  const [targetDirectory, setTargetDirectory] = useState('');
  const [loading, setLoading] = useState(false);
  const [run, setRun] = useState(null);
  const [runs, setRuns] = useState([]);
  const [graphLoading, setGraphLoading] = useState(false);
  const [graphOpen, setGraphOpen] = useState(false);
  const [graphData, setGraphData] = useState(null);
  const [graphRequest, setGraphRequest] = useState(null);
  const currentRunIdRef = useRef(null);

  useEffect(() => {
    currentRunIdRef.current = run?.id || null;
  }, [run]);

  useEffect(() => {
    const connection = songId.createHub();
    let active = true;

    connection.on('LIST', (runs) => {
      if (active && Array.isArray(runs)) {
        setRuns(runs);
        if (runs.length > 0 && !currentRunIdRef.current) {
          setRun(runs[0]);
        }
      }
    });

    connection.on('CREATE', (nextRun) => {
      if (active && nextRun?.id) {
        setRuns((currentRuns) => {
          const existing = currentRuns.filter((item) => item.id !== nextRun.id);
          return [nextRun, ...existing].slice(0, 25);
        });
        if (nextRun.id === currentRunIdRef.current || !currentRunIdRef.current) {
          setRun(nextRun);
        }
      }
    });

    connection.on('UPDATE', (nextRun) => {
      if (active && nextRun?.id) {
        setRuns((currentRuns) => {
          const existing = currentRuns.filter((item) => item.id !== nextRun.id);
          return [nextRun, ...existing]
            .sort((left, right) => new Date(right.createdAt) - new Date(left.createdAt))
            .slice(0, 25);
        });
        if (nextRun.id === currentRunIdRef.current) {
          setRun(nextRun);
        }
      }
    });

    connection.start().catch((error) => {
      console.warn('SongID hub connection failed', error);
    });

    return () => {
      active = false;
      connection.stop().catch(() => {});
    };
  }, []);

  const handleAnalyze = async () => {
    const trimmed = source.trim();
    if (!trimmed) {
      toast.error('Provide a URL, server-side file path, or text query');
      return;
    }

    setLoading(true);
    try {
      const result = await songId.createRun(trimmed);
      setRun(result);
      setRuns((currentRuns) => [result, ...currentRuns.filter((item) => item.id !== result.id)].slice(0, 25));
      toast.success('SongID analysis queued');
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'SongID analysis failed',
      );
    } finally {
      setLoading(false);
    }
  };

  const handleTrackSearch = async (candidate) => {
    try {
      await searches.create({
        id: uuidv4(),
        searchText: candidate.searchText,
      });
      toast.success(`Started search for ${candidate.artist} - ${candidate.title}`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to start song search',
      );
    }
  };

  const handleTrackSearchBatch = async (queries) => {
    const validQueries = Array.isArray(queries)
      ? queries.map((query) => (query || '').trim()).filter(Boolean)
      : [];
    if (validQueries.length === 0) {
      toast.error('No candidate searches were available');
      return;
    }

    try {
      const count = await searches.createBatch({ queries: validQueries });
      toast.success(`Started ${count} candidate searches`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to start candidate searches',
      );
    }
  };

  const handleMixSearch = async (mix) => {
    if (!mix?.searchText) {
      toast.error('No mix queries were available');
      return;
    }

    const queries = mix.searchText
      .split('+')
      .map((value) => (value || '').trim())
      .filter(Boolean);
    if (queries.length === 0) {
      toast.error('No mix queries were available');
      return;
    }

    try {
      const count = await searches.createBatch({ queries });
      toast.success(`Started ${count} mix search(es) for ${mix.segmentCount} segments`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to start mix searches',
      );
    }
  };

  const handleAlbumPrepare = async (candidate) => {
    try {
      await musicBrainz.resolveTarget({ releaseId: candidate.releaseId });
      toast.success(`Prepared album target for ${candidate.title}`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to prepare album target',
      );
    }
  };

  const handleDiscography = async (candidate) => {
    try {
      const response = await jobs.createDiscographyJob({
        artistId: candidate.artistId,
        profile: candidate.recommendedProfile || 'CoreDiscography',
        targetDirectory: targetDirectory.trim(),
      });
      toast.success(`Planned discography job ${response?.job_id}`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to create discography job',
      );
    }
  };

  const handleMbReleaseJob = async (candidate) => {
    try {
      const response = await jobs.createMbReleaseJob({
        mbReleaseId: candidate.releaseId,
        targetDir: targetDirectory.trim(),
      });
      toast.success(`Planned album job ${response?.job_id}`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to create album download job',
      );
    }
  };

  const copyForensicMatrix = async () => {
    if (!run?.id || !run?.forensicMatrix) {
      toast.error('No forensic matrix is available for this SongID run');
      return;
    }

    try {
      const matrix = await songId.getForensicMatrix(run.id);
      await navigator.clipboard.writeText(JSON.stringify(matrix, null, 2));
      toast.success('SongID forensic matrix copied');
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          'Failed to copy SongID forensic matrix',
      );
    }
  };

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
        error?.response?.data ??
          error?.message ??
          'Failed to build discovery graph',
      );
      setGraphOpen(false);
    } finally {
      setGraphLoading(false);
    }
  };

  const handleGraphRecenter = async (nodeId) => {
    if (!nodeId) {
      return;
    }

    const [nodeType, rawId] = nodeId.split(':');
    const nextRequest = {
      songIdRunId: run?.id,
    };

    if (nodeType === 'track') {
      nextRequest.scope = 'track';
      nextRequest.recordingId = rawId;
    } else if (nodeType === 'album' || nodeType === 'release-group') {
      nextRequest.scope = 'album';
      nextRequest.releaseId = rawId;
    } else if (nodeType === 'artist') {
      nextRequest.scope = 'artist';
      nextRequest.artistId = rawId;
    } else {
      nextRequest.scope = graphRequest?.scope || 'songid_run';
    }

    await openDiscoveryGraph(nextRequest);
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

  const handleQueueNearbyFromGraph = async (graph) => {
    const nodes = Array.isArray(graph?.nodes) ? graph.nodes : [];
    const queries = nodes
      .filter((node) => node.nodeType === 'track')
      .map((node) => {
        const recordingId = node.nodeId.split(':')[1];
        const candidate = (run?.tracks || []).find((item) => item.recordingId === recordingId);
        if (candidate?.searchText) {
          return candidate.searchText;
        }

        return node.label || '';
      })
      .filter(Boolean)
      .slice(0, 8);

    await handleTrackSearchBatch(queries);
  };

  const handlePlanAction = async (plan) => {
    if (plan.kind === 'track') {
      await handleTrackSearch({
        artist: plan.title.split(' - ')[0] || '',
        title: plan.title.split(' - ').slice(1).join(' - ') || plan.title,
        searchText: plan.searchText,
      });
      return;
    }

    if (plan.kind === 'album') {
      await handleAlbumPrepare({ releaseId: plan.targetId, title: plan.title });
      return;
    }

    if (plan.kind === 'artist') {
      await handleDiscography({
        artistId: plan.targetId,
        recommendedProfile: plan.profile,
      });
    }
  };

  const handleOptionAction = async (option) => {
    if (option.actionKind === 'track_search') {
      const segments = option.title.split(' - ');
      await handleTrackSearch({
        artist: segments[0] || '',
        title: segments.slice(1).join(' - ') || option.title,
        searchText: option.searchText,
      });
      return;
    }

    if (option.actionKind === 'track_search_batch') {
      await handleTrackSearchBatch(option.searchTexts);
      return;
    }

    if (option.actionKind === 'album_prepare') {
      await handleAlbumPrepare({ releaseId: option.targetId, title: option.title });
      return;
    }

    if (option.actionKind === 'mb_release_job') {
      await handleMbReleaseJob({ releaseId: option.targetId });
      return;
    }

    if (option.actionKind === 'discography_job') {
      await handleDiscography({
        artistId: option.targetId,
        recommendedProfile: option.profile,
      });
    }
  };

  const renderScores = (item) => (
    <div style={{ marginTop: '0.35em' }}>
      <Label size="tiny">Identity {Math.round((item.identityScore || 0) * 100)}</Label>
      <Label size="tiny">Byzantine {Math.round((item.byzantineScore || 0) * 100)}</Label>
      {item.canonicalScore ? (
        <Label color="teal" size="tiny">
          Canonical {Math.round((item.canonicalScore || 0) * 100)}
        </Label>
      ) : null}
      <Label color="blue" size="tiny">
        Action {Math.round((item.actionScore || 0) * 100)}
      </Label>
    </div>
  );

  const renderOptionScores = (item) => (
    <div style={{ marginTop: '0.35em' }}>
      <Label color="blue" size="tiny">
        Overall {Math.round((item.overallScore || 0) * 100)}
      </Label>
      <Label size="tiny">Quality {Math.round((item.qualityScore || 0) * 100)}</Label>
      <Label size="tiny">Byzantine {Math.round((item.byzantineScore || 0) * 100)}</Label>
      <Label size="tiny">Ready {Math.round((item.readinessScore || 0) * 100)}</Label>
    </div>
  );

  const renderSyntheticPopupContent = () => {
    if (!run?.forensicMatrix) {
      return run?.syntheticAssessment?.summary || 'No synthetic detail available.';
    }

    const matrix = run.forensicMatrix;
    return (
      <div style={{ maxWidth: 360 }}>
        <div>
          Synthetic score {matrix.syntheticScore || 0} · confidence{' '}
          {matrix.confidenceScore || 0} · family {matrix.familyLabel || 'none'}
        </div>
        {Array.isArray(matrix.topEvidenceFor) && matrix.topEvidenceFor.length > 0 ? (
          <div style={{ marginTop: '0.5em' }}>
            For: {matrix.topEvidenceFor.slice(0, 3).join(' | ')}
          </div>
        ) : null}
        {Array.isArray(matrix.topEvidenceAgainst) &&
        matrix.topEvidenceAgainst.length > 0 ? (
          <div style={{ marginTop: '0.5em' }}>
            Against: {matrix.topEvidenceAgainst.slice(0, 3).join(' | ')}
          </div>
        ) : null}
        {Array.isArray(matrix.notes) && matrix.notes.length > 0 ? (
          <div style={{ marginTop: '0.5em' }}>
            Notes: {matrix.notes.slice(0, 4).join(' | ')}
          </div>
        ) : null}
      </div>
    );
  };

  const renderLane = (label, lane) => {
    if (!lane) {
      return null;
    }

    const metricSummary = Object.entries(lane.metrics || {})
      .slice(0, 5)
      .map(([key, value]) => `${key}: ${value}`)
      .join(' | ');

    return (
      <Popup
        key={label}
        content={
          <div style={{ maxWidth: 360 }}>
            <div>{lane.summary || 'No lane summary.'}</div>
            {metricSummary ? (
              <div style={{ marginTop: '0.5em' }}>{metricSummary}</div>
            ) : null}
          </div>
        }
        position="top left"
        trigger={
          <Label size="tiny">
            {label} {Math.round((lane.score || 0) * 100)}
          </Label>
        }
      />
    );
  };

  const tracks = getDedupedTracks(run?.tracks);
  const options = getDedupedOptions(run?.options);
  const plans = getDedupedPlans(run?.plans);
  const bestTrack = tracks[0];
  const bestAlbum = Array.isArray(run?.albums) ? run.albums[0] : null;
  const bestArtist = Array.isArray(run?.artists) ? run.artists[0] : null;
  const topActions = getUniqueTopActions([
    bestTrack ? {
      label: 'Search Best Track',
      track: bestTrack,
      type: 'track',
    } : null,
    bestAlbum ? {
      album: bestAlbum,
      label: 'Download Best Album',
      type: 'album',
    } : null,
    bestArtist ? {
      artist: bestArtist,
      label: 'Plan Best Artist',
      type: 'artist',
    } : null,
    ...options.slice(0, 4).map((option) => ({
      label: option.actionLabel,
      option,
      type: 'option',
    })),
    ...plans.slice(0, 4).map((plan) => ({
      label: plan.actionLabel,
      plan,
      type: 'plan',
    })),
  ].filter(Boolean), 5);
  const identityVerdict = run?.identityAssessment?.verdict || run?.assessment?.verdict;
  const identityConfidence = (run?.identityAssessment?.confidence ?? run?.assessment?.confidence) || 0;
  const identitySummary = run?.identityAssessment?.summary || run?.assessment?.summary;
  const showIdentity = Boolean(identitySummary || identityConfidence > 0 || (identityVerdict && identityVerdict !== 'unclassified'));
  const showSynthetic = Boolean(
    run?.syntheticAssessment?.summary ||
    (run?.syntheticAssessment?.verdict && run.syntheticAssessment.verdict !== 'insufficient_evidence') ||
    run?.forensicMatrix?.familyLabel ||
    hasScorecardSignal(run?.scorecard),
  );

  return (
    <>
    <Segment
      className="songid-panel"
      raised
    >
      <Header as="h4">SongID</Header>
      <p style={{ marginTop: 0 }}>
        Identify a likely track, album, or artist from a YouTube URL, Spotify
        URL, server-side file path, or direct text query, then fan the result
        out into slskdN actions.
      </p>
      <Form>
        <Form.Field>
          <Input
            disabled={disabled || loading}
            onChange={(event) => setSource(event.target.value)}
            placeholder="Paste a source URL, local server path, or text query"
            value={source}
          />
        </Form.Field>
        <Form.Field>
          <Input
            disabled={disabled || loading}
            onChange={(event) => setTargetDirectory(event.target.value)}
            placeholder="Optional target directory for album or discography jobs"
            value={targetDirectory}
          />
        </Form.Field>
        <Popup
          content="Analyze the source and rank likely track, album, and artist candidates so you can search or plan downloads from the result."
          position="top right"
          trigger={
            <Button
              disabled={disabled || loading}
              loading={loading}
              onClick={handleAnalyze}
              primary
            >
              Analyze with SongID
            </Button>
          }
        />
      </Form>

      <Grid stackable columns={2} style={{ marginTop: '1em' }}>
        <Grid.Column width={5}>
          <Segment
            className="songid-queue-panel"
            secondary
          >
            <Header as="h5" style={{ marginTop: 0 }}>
              Queue
            </Header>
            {runs.length === 0 ? (
              <p style={{ marginBottom: 0 }}>No SongID runs yet.</p>
            ) : (
              <List divided relaxed>
                {runs.map((item) => (
                  <List.Item key={item.id}>
                    <List.Content floated="right">
                      <Popup
                        content="Open this SongID run and follow its queue position, evidence, and download options."
                        position="top center"
                        trigger={
                          <Button
                            size="mini"
                            onClick={() => setRun(item)}
                            primary={run?.id === item.id}
                          >
                            View
                          </Button>
                        }
                      />
                      <Popup
                        content="Open the Discovery Graph centered on this SongID run so you can inspect its neighborhood without leaving the queue."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() =>
                              openDiscoveryGraph({
                                scope: 'songid_run',
                                songIdRunId: item.id,
                                title: item.query || item.summary,
                              })
                            }
                            size="mini"
                            style={{ marginLeft: '0.5em' }}
                          >
                            Graph
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>{getRunTitle(item)}</List.Header>
                      <List.Description>
                        <Label
                          color={getStatusColor(item.status)}
                          size="tiny"
                        >
                          {item.status || 'unknown'}
                        </Label>
                        <Label size="tiny">{item.sourceType || 'unknown'}</Label>
                        {item.percentComplete ? (
                          <Label size="tiny">
                            {formatPercent(item.percentComplete)}%
                          </Label>
                        ) : null}
                      </List.Description>
                      <List.Description style={{ marginTop: '0.35em' }}>
                        {item.queuePosition !== null &&
                        item.queuePosition !== undefined ? (
                          <Label size="tiny">Queue {item.queuePosition}</Label>
                        ) : null}
                        {item.workerSlot !== null &&
                        item.workerSlot !== undefined ? (
                          <Label size="tiny">Worker {item.workerSlot}</Label>
                        ) : null}
                        {item.currentStage ? (
                          <Label size="tiny">{item.currentStage}</Label>
                        ) : null}
                      </List.Description>
                      {item.summary ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          {item.summary}
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            )}
          </Segment>
        </Grid.Column>
        <Grid.Column width={11}>
      {run ? (
            <Segment
              className="songid-result-panel"
              secondary
            >
          <Header as="h5" style={{ marginTop: 0 }}>
            Result
          </Header>
          <div style={{ marginBottom: '1em' }}>
            <Popup
              content="Open the Discovery Graph for this SongID run and move sideways through nearby tracks, albums, artists, and decomposed segments."
              position="top center"
              trigger={
                <Button
                  onClick={() =>
                    openDiscoveryGraph({
                      scope: 'songid_run',
                      songIdRunId: run?.id,
                      title: run?.query || run?.metadata?.title,
                    })
                  }
                  size="small"
                >
                  Open Discovery Graph
                </Button>
              }
            />
          </div>
                  <Segment className="songid-result-summary">
            <Header as="h4" style={{ marginTop: 0 }}>
              {bestTrack
                ? `${bestTrack.artist} - ${bestTrack.title}`
                : getRunTitle(run)}
            </Header>
            <div style={{ marginBottom: '0.75em' }}>
              <Label color={getStatusColor(run.status)} size="small">
                {run.status || 'unknown'}
              </Label>
              <Label size="small">{run.currentStage || 'queued'}</Label>
              <Label size="small">{run.sourceType || 'unknown'}</Label>
              {run.workerSlot !== null && run.workerSlot !== undefined ? (
                <Label size="small">Worker {run.workerSlot}</Label>
              ) : null}
              {run.queuePosition !== null && run.queuePosition !== undefined ? (
                <Label size="small">Queue {run.queuePosition}</Label>
              ) : null}
            </div>
            {run.summary ? <p>{run.summary}</p> : null}
            {run.query ? (
              <p style={{ marginBottom: 0 }}>
                <strong>Query:</strong> {run.query}
              </p>
            ) : null}
          </Segment>
          <Progress
            color={run.status === 'failed' ? 'red' : 'blue'}
            percent={Math.round((run.percentComplete || 0) * 100)}
            precision={0}
            progress
            size="small"
          >
            {run.currentStage || 'queued'}
          </Progress>
          {topActions.length > 0 ? (
            <>
              <Header as="h5">Best Next Actions</Header>
              <div style={{ marginBottom: '1em' }}>
                {topActions.map((action, index) => {
                  const content = {
                    album: 'Create a single-release job from the strongest album candidate.',
                    artist: 'Create a discography job from the strongest artist candidate.',
                    option: 'Run this ranked SongID acquisition option.',
                    plan: 'Run this ranked SongID plan.',
                    track: 'Start a regular slskdN search using the strongest SongID track candidate.',
                  }[action.type];
                  const onClick = () => {
                    if (action.type === 'track') {
                      return handleTrackSearch(action.track);
                    }
                    if (action.type === 'album') {
                      return handleMbReleaseJob(action.album);
                    }
                    if (action.type === 'artist') {
                      return handleDiscography(action.artist);
                    }
                    if (action.type === 'option') {
                      return handleOptionAction(action.option);
                    }

                    return handlePlanAction(action.plan);
                  };

                  return (
                    <Popup
                      key={`${action.type}-${action.label}-${index}`}
                      content={content}
                      position="top center"
                      trigger={
                        <Button
                          onClick={onClick}
                          primary={index === 0}
                          size="small"
                          style={{ marginLeft: index === 0 ? 0 : '0.5em' }}
                        >
                          {action.label}
                        </Button>
                      }
                    />
                  );
                })}
              </div>
            </>
          ) : null}
          {graphData && graphRequest?.songIdRunId === run?.id ? (
            <>
              <Divider />
              <Header as="h5">Mini-Map</Header>
              <Segment secondary>
                <DiscoveryGraphCanvas
                  graph={graphData}
                  height={220}
                  onNodeClick={handleGraphRecenter}
                  width={520}
                />
              </Segment>
            </>
          ) : null}

          {Array.isArray(run.mixGroups) && run.mixGroups.length > 0 ? (
            <>
              <Header as="h5">Mix Clusters</Header>
              <List divided relaxed>
                {run.mixGroups.map((mix) => (
                  <List.Item key={mix.mixId}>
                    <List.Content floated="right">
                      <Popup
                        content="Queue the mix cluster segments as a batch of SongID searches."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() => handleMixSearch(mix)}
                            size="small"
                          >
                            Search Mix
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>{mix.label}</List.Header>
                      <List.Description>
                        {mix.segmentCount} segments · confidence {Math.round((mix.confidence || 0) * 100)}
                      </List.Description>
                      <List.Description>
                        Identity {Math.round((mix.identityScore || 0) * 100)} · Byzantine {Math.round((mix.byzantineScore || 0) * 100)}
                      </List.Description>
                      {mix.searchText ? (
                        <List.Description>
                          Query: {mix.searchText}
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {showIdentity ? (
            <>
              <Header as="h5">Identity</Header>
              <p>
                <strong>Verdict:</strong>{' '}
                <Label
                  color={getIdentityVerdictColor(identityVerdict)}
                  size="tiny"
                >
                  {identityVerdict || 'unclassified'}
                </Label>
                <br />
                <strong>Confidence:</strong> {formatPercent(identityConfidence)}
                <br />
                <strong>Reasoning:</strong> {identitySummary || 'None'}
              </p>
            </>
          ) : null}

          {showSynthetic ? (
            <>
              <Header as="h5">Synthetic Signals</Header>
              <div style={{ marginBottom: '1em' }}>
                <Popup
                  content={renderSyntheticPopupContent()}
                  position="top left"
                  trigger={
                    <Label
                      color={getSyntheticVerdictColor(
                        run.syntheticAssessment?.verdict,
                      )}
                      size="tiny"
                    >
                      Synthetic {run.syntheticAssessment?.verdict || 'unknown'}
                    </Label>
                  }
                />
                <Popup
                  content="Confidence applies to the synthetic-likelihood readout only. It does not control download decisions when identity is strong."
                  position="top left"
                  trigger={
                    <Label size="tiny">
                      Confidence{' '}
                      {run.forensicMatrix?.confidenceScore ||
                        run.syntheticAssessment?.confidence ||
                        'low'}
                    </Label>
                  }
                />
                {run.forensicMatrix?.familyLabel ? (
                  <Popup
                    content={`Known-family score ${run.forensicMatrix.knownFamilyScore || 0}. This is a hint lane, not a download-control signal.`}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Family {run.forensicMatrix.familyLabel}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix?.qualityClass ? (
                  <Label size="tiny">
                    Quality {run.forensicMatrix.qualityClass}
                  </Label>
                ) : null}
                {run.forensicMatrix?.perturbationStability !== undefined ? (
                  <Label size="tiny">
                    Stability{' '}
                    {Math.round(
                      (run.forensicMatrix.perturbationStability || 0) * 100,
                    )}
                  </Label>
                ) : null}
              </div>
              {run.syntheticAssessment?.summary ? (
                <p style={{ marginTop: 0 }}>
                  {run.syntheticAssessment.summary}
                </p>
              ) : null}
              <div style={{ marginBottom: '1em' }}>
                {renderLane('Provenance', run.forensicMatrix?.provenanceLane)}
                {renderLane('Spectral', run.forensicMatrix?.spectralArtifactLane)}
                {renderLane('Descriptor', run.forensicMatrix?.descriptorPriorsLane)}
                {renderLane('Lyrics', run.forensicMatrix?.lyricsSpeechLane)}
                {renderLane('Structure', run.forensicMatrix?.structuralLane)}
                {renderLane('Family', run.forensicMatrix?.generatorFamilyLane)}
              </div>
            </>
          ) : null}

          {hasScorecardSignal(run.scorecard) ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>Scorecard</summary>
              <div style={{ marginBottom: '1em' }}>
                <Label size="tiny">Audio {run.scorecard.analysisAudioSource || 'none'}</Label>
                <Label size="tiny">Clips {run.scorecard.clipCount || 0}</Label>
                <Label size="tiny">AcoustID {run.scorecard.acoustIdHitCount || 0}</Label>
                <Label size="tiny">AcoustID Raw {run.scorecard.rawAcoustIdHitCount || 0}</Label>
                <Label size="tiny">SongRec {run.scorecard.songRecHitCount || 0}</Label>
                <Label size="tiny">SongRec Distinct {run.scorecard.songRecDistinctMatchCount || 0}</Label>
                <Label size="tiny">Transcript {run.scorecard.transcriptCount || 0}</Label>
                <Label size="tiny">OCR {run.scorecard.ocrCount || 0}</Label>
                <Label size="tiny">Comments {run.scorecard.commentFindingCount || 0}</Label>
                <Label size="tiny">Timestamps {run.scorecard.timestampHintCount || 0}</Label>
                <Label size="tiny">Chapters {run.scorecard.chapterHintCount || 0}</Label>
                <Label size="tiny">Playlist Requests {run.scorecard.playlistRequestCount || 0}</Label>
                <Label size="tiny">AI Mentions {run.scorecard.aiCommentMentionCount || 0}</Label>
                <Label size="tiny">Panako {run.scorecard.panakoHitCount || 0}</Label>
                <Label size="tiny">Audfprint {run.scorecard.audfprintHitCount || 0}</Label>
                <Label size="tiny">Corpus {run.scorecard.corpusMatchCount || 0}</Label>
                <Label size="tiny">
                  Provenance {run.scorecard.provenanceSignalCount || 0}
                </Label>
                <Label size="tiny">
                  AI Heuristics {run.scorecard.aiArtifactClipCount || 0}
                </Label>
              </div>
              {Array.isArray(run.scorecard.embeddedMetadataKeys) &&
              run.scorecard.embeddedMetadataKeys.length > 0 ? (
                <div style={{ marginBottom: '1em' }}>
                  <strong>Metadata Keys:</strong>{' '}
                  {run.scorecard.embeddedMetadataKeys.join(', ')}
                </div>
              ) : null}
              {Array.isArray(run.scorecard.provenanceSignals) &&
              run.scorecard.provenanceSignals.length > 0 ? (
                <div style={{ marginBottom: '1em' }}>
                  <strong>Provenance Signals:</strong>{' '}
                  {run.scorecard.provenanceSignals.join(', ')}
                </div>
              ) : null}
            </details>
          ) : null}

          {run.fullSourceFingerprint ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>Full-Source Fingerprint</summary>
              <p>
                <strong>Duration:</strong>{' '}
                {Math.round(run.fullSourceFingerprint.durationSeconds || 0)}s
                <br />
                <strong>Fingerprint Length:</strong>{' '}
                {run.fullSourceFingerprint.fingerprintLength || 0}
                <br />
                <strong>Path:</strong> {run.fullSourceFingerprint.path}
              </p>
            </details>
          ) : null}

          {run.provenance && run.provenance.signalCount > 0 ? (
            <>
              <Header as="h5">Provenance</Header>
              <div style={{ marginBottom: '0.75em' }}>
                <Label size="tiny">
                  Tool {run.provenance.toolAvailable ? 'available' : 'unavailable'}
                </Label>
                <Label size="tiny">
                  Manifest {run.provenance.manifestHint ? 'hinted' : 'none'}
                </Label>
                <Label size="tiny">
                  Validation {run.provenance.validationState || 'unknown'}
                </Label>
                {run.provenance.verified ? (
                  <Label color="green" size="tiny">
                    verified
                  </Label>
                ) : null}
              </div>
              <List bulleted>
                {(run.provenance.signals || []).map((signal) => (
                  <List.Item key={signal}>{signal}</List.Item>
                ))}
              </List>
            </>
          ) : null}

          {run.aiHeuristics ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>AI Audio Heuristics</summary>
              <div style={{ marginBottom: '1em' }}>
                <Label size="tiny">
                  Score {Math.round((run.aiHeuristics.artifactScore || 0) * 100)}
                </Label>
                <Label size="tiny">{run.aiHeuristics.artifactLabel || 'unknown'}</Label>
                <Label size="tiny">
                  Peaks {run.aiHeuristics.peakCount || 0}
                </Label>
                <Label size="tiny">
                  Periodicity{' '}
                  {Math.round((run.aiHeuristics.periodicityStrength || 0) * 100)}
                </Label>
                <Label size="tiny">
                  Centroid {Math.round(run.aiHeuristics.spectralCentroid || 0)}
                </Label>
                <Label size="tiny">
                  Flux {Math.round((run.aiHeuristics.spectralFlux || 0) * 1000)}
                </Label>
                <Label size="tiny">
                  Pitch {Math.round((run.aiHeuristics.pitchSalience || 0) * 100)}
                </Label>
              </div>
            </details>
          ) : null}

          {Array.isArray(run.perturbations) && run.perturbations.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Perturbation Stability ({run.perturbations.length})
              </summary>
              <List divided relaxed>
                {run.perturbations.map((item) => (
                  <List.Item key={item.perturbationId}>
                    <List.Content>
                      <List.Header>{item.label}</List.Header>
                      <List.Description>
                        Delta {Math.round((item.baselineDelta || 0) * 100)}
                      </List.Description>
                      {item.heuristics ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Score {Math.round((item.heuristics.artifactScore || 0) * 100)} ·
                          {` `}Label {item.heuristics.artifactLabel || 'unknown'}
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.corpusMatches) && run.corpusMatches.length > 0 ? (
            <>
              <Header as="h5">Corpus Matches</Header>
              <List divided relaxed>
                {run.corpusMatches.map((match) => (
                  <List.Item key={match.matchId}>
                    <List.Content>
                      <List.Header>{match.label || match.source}</List.Header>
                      <List.Description>
                        Similarity {Math.round((match.similarityScore || 0) * 100)}
                      </List.Description>
                      {match.artist || match.title ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          {[match.artist, match.title].filter(Boolean).join(' - ')}
                        </List.Description>
                      ) : null}
                      {match.recordingId ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Recording: {match.recordingId}
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {Array.isArray(run.stems) && run.stems.length > 0 ? (
            <>
              <Header as="h5">Demucs Stems</Header>
              <List bulleted>
                {run.stems.map((stem) => (
                  <List.Item key={stem.artifactId}>
                    {stem.label}: {stem.path}
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {Array.isArray(run.evidence) && run.evidence.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Evidence ({run.evidence.length})
              </summary>
              <List bulleted>
                {run.evidence.map((item) => (
                  <List.Item key={item}>{item}</List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.segments) && run.segments.length > 0 ? (
            <>
              <Header as="h5">Segment Decomposition</Header>
              <List divided relaxed>
                {run.segments.map((segment) => (
                  <List.Item key={segment.segmentId}>
                    <List.Content>
                      <List.Header>
                        {segment.label}{' '}
                        <Label size="tiny">
                          {Math.round((segment.confidence || 0) * 100)}%
                        </Label>
                      </List.Header>
                      <List.Description>
                        {segment.decompositionLabel || segment.sourceLabel}
                      </List.Description>
                      <List.Description style={{ marginTop: '0.35em' }}>
                        Search: <code>{segment.query}</code>
                      </List.Description>
                      {Array.isArray(segment.candidates) &&
                      segment.candidates.length > 0 ? (
                        <List.Description style={{ marginTop: '0.5em' }}>
                          {segment.candidates.map((candidate) => (
                            <Label key={candidate.candidateId} size="tiny">
                              {candidate.artist} - {candidate.title}
                            </Label>
                          ))}
                        </List.Description>
                      ) : null}
                      <div style={{ marginTop: '0.5em' }}>
                        {Array.isArray(segment.options) &&
                        segment.options.length > 0 ? (
                          <Popup
                            content="Run the best segment-level SongID action for this decomposed portion of the source."
                            position="top center"
                            trigger={
                              <Button
                                onClick={() => handleOptionAction(segment.options[0])}
                                size="mini"
                              >
                                {segment.options[0].actionLabel}
                              </Button>
                            }
                          />
                        ) : (
                          <Popup
                            content="Search this decomposed segment directly when SongID found a plausible chapter or timestamp clue."
                            position="top center"
                            trigger={
                              <Button
                                onClick={() =>
                                  handleTrackSearchBatch([segment.query])
                                }
                                size="mini"
                              >
                                Search Segment
                              </Button>
                            }
                          />
                        )}
                      </div>
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {options.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Ranked Download Options ({options.length})
              </summary>
              <List divided relaxed>
                {options.map((option) => (
                  <List.Item key={option.optionId}>
                    <List.Content floated="right">
                      <Popup
                        content="Run this SongID acquisition option using the scored track, album, or discography path shown here."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() => handleOptionAction(option)}
                            size="small"
                          >
                            {option.actionLabel}
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>
                        {option.title}{' '}
                        <Label size="tiny">{option.scope}</Label>
                        <Label size="tiny">{option.mode}</Label>
                        {option.duplicateCount > 1 ? (
                          <Label color="grey" size="tiny">
                            {option.duplicateCount} matches
                          </Label>
                        ) : null}
                      </List.Header>
                      <List.Description>{option.description}</List.Description>
                      {option.searchText ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Search: <code>{option.searchText}</code>
                        </List.Description>
                      ) : null}
                      {Array.isArray(option.searchTexts) &&
                      option.searchTexts.length > 0 ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Searches: <code>{option.searchTexts.join(' | ')}</code>
                        </List.Description>
                      ) : null}
                      {option.profile ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Profile: {option.profile}
                        </List.Description>
                      ) : null}
                      {renderOptionScores(option)}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.clips) && run.clips.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Clip Findings ({run.clips.length})
              </summary>
              <List divided relaxed>
                {run.clips.map((clip) => (
                  <List.Item key={clip.clipId}>
                    <List.Content>
                      <List.Header>
                        {clip.profile} @ {clip.startSeconds}s
                      </List.Header>
                      <List.Description>
                        {clip.durationSeconds}s clip
                      </List.Description>
                      {clip.acoustId ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          AcoustID: {clip.acoustId.artist} - {clip.acoustId.title}
                        </List.Description>
                      ) : null}
                      {clip.songRec ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          SongRec: {clip.songRec.artist} - {clip.songRec.title}
                        </List.Description>
                      ) : null}
                      {clip.panako ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Panako: {clip.panako.title || clip.panako.sourcePath}
                        </List.Description>
                      ) : null}
                      {clip.audfprint ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Audfprint: {clip.audfprint.title}
                        </List.Description>
                      ) : null}
                      {clip.aiHeuristics ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          AI Heuristics: {clip.aiHeuristics.artifactLabel} (
                          {Math.round((clip.aiHeuristics.artifactScore || 0) * 100)})
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.transcripts) && run.transcripts.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Transcripts ({run.transcripts.length})
              </summary>
              <List divided relaxed>
                {run.transcripts.map((transcript) => (
                  <List.Item key={transcript.transcriptId}>
                    <List.Content>
                      <List.Header>{transcript.source}</List.Header>
                      <List.Description>{transcript.text}</List.Description>
                      <List.Description style={{ marginTop: '0.35em' }}>
                        Segments: {transcript.segmentCount || 0} · Language:{' '}
                        {transcript.language || 'unknown'} · Excerpt:{' '}
                        {transcript.excerptStartSeconds || 0}s /{' '}
                        {transcript.excerptDurationSeconds || 0}s
                      </List.Description>
                      {Array.isArray(transcript.musicBrainzQueries) &&
                      transcript.musicBrainzQueries.length > 0 ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Queries: {transcript.musicBrainzQueries.join(' | ')}
                        </List.Description>
                      ) : null}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.ocr) && run.ocr.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>OCR ({run.ocr.length})</summary>
              <List bulleted>
                {run.ocr.map((item) => (
                  <List.Item key={item.ocrId}>
                    {item.timestampSeconds}s: {item.text}
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.comments) && run.comments.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Comments ({run.comments.length})
              </summary>
              <List bulleted>
                {run.comments.map((comment) => (
                  <List.Item key={comment.commentId}>
                    {comment.author ? `${comment.author}: ` : ''}
                    {comment.text}
                    {comment.timestampSeconds !== null &&
                    comment.timestampSeconds !== undefined
                      ? ` (${comment.timestampSeconds}s)`
                      : ''}
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {Array.isArray(run.chapters) && run.chapters.length > 0 ? (
            <>
              <Header as="h5">Chapters</Header>
              <List bulleted>
                {run.chapters.map((chapter) => (
                  <List.Item key={chapter.chapterId}>
                    {chapter.startSeconds}s: {chapter.title || 'untitled chapter'}
                    {chapter.endSeconds !== null &&
                    chapter.endSeconds !== undefined
                      ? ` -> ${chapter.endSeconds}s`
                      : ''}
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {run.forensicMatrix ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>Forensic Matrix</summary>
              <div style={{ marginBottom: '1em' }}>
                <Popup
                  content="Copy the complete forensic matrix JSON for this SongID run without changing ranking, downloads, or local files."
                  position="top left"
                  trigger={
                    <Button
                      aria-label="Copy SongID forensic matrix JSON"
                      icon="copy"
                      onClick={copyForensicMatrix}
                      size="mini"
                    />
                  }
                />
              </div>
              <div style={{ marginBottom: '1em' }}>
                <Label size="tiny">Identity {run.forensicMatrix.identityScore || 0}</Label>
                <Label size="tiny">
                  Synthetic {run.forensicMatrix.syntheticScore || 0}
                </Label>
                <Label size="tiny">
                  Confidence {run.forensicMatrix.confidenceScore || 0}
                </Label>
                <Label size="tiny">
                  Family Score {run.forensicMatrix.knownFamilyScore || 0}
                </Label>
              </div>
              <div style={{ marginBottom: '1em' }}>
                {run.forensicMatrix.identityLane ? (
                  <Popup
                    content={run.forensicMatrix.identityLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Identity Lane {Math.round((run.forensicMatrix.identityLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix.confidenceLane ? (
                  <Popup
                    content={run.forensicMatrix.confidenceLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Confidence Lane {Math.round((run.forensicMatrix.confidenceLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix.spectralArtifactLane ? (
                  <Popup
                    content={run.forensicMatrix.spectralArtifactLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Spectral {Math.round((run.forensicMatrix.spectralArtifactLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix.lyricsSpeechLane ? (
                  <Popup
                    content={run.forensicMatrix.lyricsSpeechLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Lyrics {Math.round((run.forensicMatrix.lyricsSpeechLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix.structuralLane ? (
                  <Popup
                    content={run.forensicMatrix.structuralLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Structure {Math.round((run.forensicMatrix.structuralLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
                {run.forensicMatrix.provenanceLane ? (
                  <Popup
                    content={run.forensicMatrix.provenanceLane.summary}
                    position="top left"
                    trigger={
                      <Label size="tiny">
                        Provenance {Math.round((run.forensicMatrix.provenanceLane.score || 0) * 100)}
                      </Label>
                    }
                  />
                ) : null}
              </div>
              {Array.isArray(run.forensicMatrix.topEvidenceFor) &&
              run.forensicMatrix.topEvidenceFor.length > 0 ? (
                <div style={{ marginBottom: '0.75em' }}>
                  <strong>For:</strong> {run.forensicMatrix.topEvidenceFor.join(' | ')}
                </div>
              ) : null}
              {Array.isArray(run.forensicMatrix.topEvidenceAgainst) &&
              run.forensicMatrix.topEvidenceAgainst.length > 0 ? (
                <div style={{ marginBottom: '0.75em' }}>
                  <strong>Against:</strong>{' '}
                  {run.forensicMatrix.topEvidenceAgainst.join(' | ')}
                </div>
              ) : null}
              {Array.isArray(run.forensicMatrix.notes) &&
              run.forensicMatrix.notes.length > 0 ? (
                <div style={{ marginBottom: '0.75em' }}>
                  <strong>Notes:</strong> {run.forensicMatrix.notes.join(' | ')}
                </div>
              ) : null}
            </details>
          ) : null}

          {plans.length > 0 ? (
            <details style={detailStyle}>
              <summary style={detailSummaryStyle}>
                Ranked Plans ({plans.length})
              </summary>
              <List divided relaxed>
                {plans.map((plan) => (
                  <List.Item key={plan.planId}>
                    <List.Content floated="right">
                      <Popup
                        content="Run the highest-value SongID action path for this candidate using the current scored plan."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() => handlePlanAction(plan)}
                            size="small"
                          >
                            {plan.actionLabel}
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>
                        {plan.title}{' '}
                        {plan.duplicateCount > 1 ? (
                          <Label color="grey" size="tiny">
                            {plan.duplicateCount} matches
                          </Label>
                        ) : null}
                      </List.Header>
                      <List.Description>{plan.subtitle}</List.Description>
                      {renderScores(plan)}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </details>
          ) : null}

          {tracks.length > 0 ? (
            <>
              <Header as="h5">Tracks</Header>
              <List divided relaxed>
                {tracks.map((candidate) => (
                  <List.Item key={candidate.candidateId}>
                    <List.Content floated="right">
                      <Popup
                        content="Start a regular slskdN search using this SongID track candidate so you can inspect sources and begin downloading."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() => handleTrackSearch(candidate)}
                            size="small"
                          >
                            Search Song
                          </Button>
                        }
                      />
                      <Popup
                        content="Open the Discovery Graph around this track candidate and explore nearby identities, ambiguity branches, and adjacent context."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() =>
                              openDiscoveryGraph({
                                scope: 'track',
                                songIdRunId: run?.id,
                                recordingId: candidate.recordingId,
                                title: candidate.title,
                                artist: candidate.artist,
                              })
                            }
                            size="small"
                            style={{ marginLeft: '0.5em' }}
                          >
                            Graph
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>
                        {candidate.artist} - {candidate.title}{' '}
                        {candidate.isExact ? (
                          <Label color="green" size="tiny">
                            exact
                          </Label>
                        ) : null}
                        {candidate.duplicateCount > 1 ? (
                          <Label color="grey" size="tiny">
                            {candidate.duplicateCount} matches
                          </Label>
                        ) : null}
                      </List.Header>
                      <List.Description>{candidate.recordingId}</List.Description>
                      {candidate.canonicalVariantCount ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Canonical variants: {candidate.canonicalVariantCount}
                          {candidate.hasLosslessCanonical ? ' · lossless available' : ''}
                        </List.Description>
                      ) : null}
                      {renderScores(candidate)}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {Array.isArray(run.albums) && run.albums.length > 0 ? (
            <>
              <Header as="h5">Albums</Header>
              <List divided relaxed>
                {run.albums.map((candidate) => (
                  <List.Item key={candidate.candidateId}>
                    <List.Content floated="right">
                      <Button.Group size="small">
                        <Popup
                          content="Resolve and cache this MusicBrainz release in slskdN so album completion and downstream download workflows can use it."
                          position="top center"
                          trigger={
                            <Button onClick={() => handleAlbumPrepare(candidate)}>
                              Prepare Album
                            </Button>
                          }
                        />
                        <Popup
                          content="Create a single-release job from this SongID album candidate so slskdN can plan album acquisition directly."
                          position="top center"
                          trigger={
                            <Button onClick={() => handleMbReleaseJob(candidate)}>
                              Download Album
                            </Button>
                          }
                        />
                        <Popup
                          content="Open the Discovery Graph around this album candidate to explore adjacent tracks, artist context, and nearby branches."
                          position="top center"
                          trigger={
                            <Button
                              onClick={() =>
                                openDiscoveryGraph({
                                  scope: 'album',
                                  songIdRunId: run?.id,
                                  releaseId: candidate.releaseId,
                                  album: candidate.title,
                                  artist: candidate.artist,
                                })
                              }
                            >
                              Graph
                            </Button>
                          }
                        />
                      </Button.Group>
                    </List.Content>
                    <List.Content>
                      <List.Header>
                        {candidate.artist} - {candidate.title}{' '}
                        {candidate.isExact ? (
                          <Label color="green" size="tiny">
                            exact
                          </Label>
                        ) : null}
                      </List.Header>
                      <List.Description>
                        {candidate.trackCount} track(s) · {candidate.releaseId}
                      </List.Description>
                      {candidate.canonicalSupportCount ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Canonical support from {candidate.canonicalSupportCount} track candidate(s)
                        </List.Description>
                      ) : null}
                      {renderScores(candidate)}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}

          {Array.isArray(run.artists) && run.artists.length > 0 ? (
            <>
              <Header as="h5">Artists</Header>
              <List divided relaxed>
                {run.artists.map((candidate) => (
                  <List.Item key={candidate.candidateId}>
                    <List.Content floated="right">
                      <Popup
                        content="Create a discography job from this SongID artist candidate so slskdN can plan a catalog-scale acquisition workflow."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() => handleDiscography(candidate)}
                            size="small"
                          >
                            Plan Discography
                          </Button>
                        }
                      />
                      <Popup
                        content="Open the Discovery Graph around this artist candidate and branch into nearby artists, releases, and SongID context."
                        position="top center"
                        trigger={
                          <Button
                            onClick={() =>
                              openDiscoveryGraph({
                                scope: 'artist',
                                songIdRunId: run?.id,
                                artistId: candidate.artistId,
                                artist: candidate.name,
                              })
                            }
                            size="small"
                            style={{ marginLeft: '0.5em' }}
                          >
                            Graph
                          </Button>
                        }
                      />
                    </List.Content>
                    <List.Content>
                      <List.Header>{candidate.name}</List.Header>
                      <List.Description>
                        {candidate.releaseGroupCount} release group(s) ·{' '}
                        {candidate.artistId}
                      </List.Description>
                      {candidate.canonicalSupportCount ? (
                        <List.Description style={{ marginTop: '0.35em' }}>
                          Canonical support from {candidate.canonicalSupportCount} track candidate(s)
                        </List.Description>
                      ) : null}
                      {renderScores(candidate)}
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </>
          ) : null}
        </Segment>
      ) : null}
        </Grid.Column>
      </Grid>
    </Segment>
    <DiscoveryGraphModal
      graph={graphData}
      loading={graphLoading}
      onCompare={handleGraphCompare}
      onClose={() => setGraphOpen(false)}
      onQueueNearby={handleQueueNearbyFromGraph}
      onRecenter={handleGraphRecenter}
      onRestoreBranch={(branch) => branch?.request && openDiscoveryGraph(branch.request)}
      open={graphOpen}
    />
    </>
  );
};

export default SongIDPanel;
