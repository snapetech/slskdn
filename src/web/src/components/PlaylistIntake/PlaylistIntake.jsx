import './PlaylistIntake.css';
import * as collectionsAPI from '../../lib/collections';
import { addDiscoveryInboxItem } from '../../lib/discoveryInbox';
import {
  addPlaylistIntake,
  applyPlaylistIntakeRefresh,
  approvePlaylistTagOrganizationPlan,
  buildPlaylistCollectionItems,
  buildPlaylistDiscoverySeed,
  buildPlaylistDiscoverySeeds,
  buildPlaylistCompletionSummary,
  buildPlaylistIntakeSummary,
  buildPlaylistProviderRefreshContent,
  buildSlskdPlaylistPreview,
  clearPlaylistTagOrganizationApproval,
  coverArtPolicies,
  formatPlaylistTagOrganizationReport,
  getDuePlaylistRefreshes,
  getPlaylistIntakes,
  markPlaylistCollectionCreated,
  multiArtistTagPolicies,
  organizationPathTemplates,
  playlistRefreshCadences,
  previewPlaylistTagOrganizationPlan,
  previewPlaylistIntakeRefresh,
  replayGainPolicies,
  updatePlaylistIntakeTrackState,
  updatePlaylistRefreshAutomation,
} from '../../lib/playlistIntake';
import * as sourceFeedImportsAPI from '../../lib/sourceFeedImports';
import React, { useMemo, useState } from 'react';
import {
  Button,
  Checkbox,
  Form,
  Header,
  Icon,
  Label,
  Popup,
  Segment,
  TextArea,
} from 'semantic-ui-react';

const trackStateColor = (state) => (state === 'Matched' ? 'green' : 'orange');

const PlaylistIntake = () => {
  const [items, setItems] = useState(() => getPlaylistIntakes());
  const [name, setName] = useState('');
  const [source, setSource] = useState('');
  const [content, setContent] = useState('');
  const [mirrorEnabled, setMirrorEnabled] = useState(false);
  const [refreshCadence, setRefreshCadence] = useState('Manual review');
  const [refreshCooldownDays, setRefreshCooldownDays] = useState(7);
  const [providerAccessToken, setProviderAccessToken] = useState('');
  const [refreshInputs, setRefreshInputs] = useState({});
  const [playlistPreviews, setPlaylistPreviews] = useState({});
  const [organizationInputs, setOrganizationInputs] = useState({});
  const [bulkSent, setBulkSent] = useState({});
  const [status, setStatus] = useState('');
  const [busyPlaylistId, setBusyPlaylistId] = useState('');

  const summary = useMemo(() => buildPlaylistIntakeSummary(items), [items]);
  const dueRefreshes = useMemo(() => getDuePlaylistRefreshes(items), [items]);

  const importPlaylist = () => {
    const sourceName = source.trim() || name.trim() || 'Pasted playlist';
    const nextItems = addPlaylistIntake({
      content,
      mirrorEnabled,
      name: name.trim() || sourceName,
      refreshCadence,
      refreshCooldownDays,
      source: sourceName,
    });

    setItems(nextItems);
    setName('');
    setSource('');
    setContent('');
    setMirrorEnabled(false);
    setRefreshCadence('Manual review');
    setRefreshCooldownDays(7);
  };

  const sendTrackToInbox = (playlist, track) => {
    addDiscoveryInboxItem(buildPlaylistDiscoverySeed(playlist, track));
  };

  const sendPlaylistRowsToInbox = (playlist) => {
    const seeds = buildPlaylistDiscoverySeeds(playlist);
    seeds.forEach((seed) => addDiscoveryInboxItem(seed));
    setBulkSent((current) => ({
      ...current,
      [playlist.id]: seeds.length,
    }));
  };

  const previewSlskdPlaylist = (playlist) => {
    setPlaylistPreviews((current) => ({
      ...current,
      [playlist.id]: buildSlskdPlaylistPreview(playlist),
    }));
  };

  const getOrganizationInput = (playlist) => ({
    albumTitle:
      organizationInputs[playlist.id]?.albumTitle ||
      playlist.organizationOptions?.albumTitle ||
      playlist.name,
    coverArtPolicy:
      organizationInputs[playlist.id]?.coverArtPolicy ||
      playlist.organizationOptions?.coverArtPolicy ||
      'sidecar',
    multiArtistPolicy:
      organizationInputs[playlist.id]?.multiArtistPolicy ||
      playlist.organizationOptions?.multiArtistPolicy ||
      'preserve',
    pathTemplate:
      organizationInputs[playlist.id]?.pathTemplate ||
      playlist.organizationOptions?.pathTemplate ||
      organizationPathTemplates[0].value,
    replayGainPolicy:
      organizationInputs[playlist.id]?.replayGainPolicy ||
      playlist.organizationOptions?.replayGainPolicy ||
      'skip',
  });

  const setOrganizationInput = (playlist, key, value) => {
    setOrganizationInputs((current) => ({
      ...current,
      [playlist.id]: {
        ...getOrganizationInput(playlist),
        ...current[playlist.id],
        [key]: value,
      },
    }));
  };

  const previewTagOrganization = (playlist) => {
    const nextItems = previewPlaylistTagOrganizationPlan(
      playlist.id,
      getOrganizationInput(playlist),
    );
    setItems(nextItems);
    setStatus(`Prepared tag and organization dry run for ${playlist.name}`);
  };

  const copyTagOrganizationReport = (playlist) => {
    const report = formatPlaylistTagOrganizationReport(playlist);
    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(report).catch(() => {});
    }
    setStatus(`Prepared tag and organization report for ${playlist.name}`);
  };

  const approveTagOrganizationSnapshot = (playlist) => {
    setItems(approvePlaylistTagOrganizationPlan(playlist.id));
    setStatus(`Approved tag and organization snapshot for ${playlist.name}`);
  };

  const clearTagOrganizationSnapshot = (playlist) => {
    setItems(clearPlaylistTagOrganizationApproval(playlist.id));
    setStatus(`Cleared tag and organization snapshot for ${playlist.name}`);
  };

  const setTrackState = (playlist, track, state) => {
    setItems(updatePlaylistIntakeTrackState(playlist.id, track.id, state));
  };

  const setRefreshInput = (playlist, value) => {
    setRefreshInputs((current) => ({
      ...current,
      [playlist.id]: value,
    }));
  };

  const previewRefresh = (playlist) => {
    setItems(previewPlaylistIntakeRefresh(playlist.id, refreshInputs[playlist.id] || ''));
  };

  const applyManualRefresh = (playlist) => {
    setItems(
      applyPlaylistIntakeRefresh(playlist.id, refreshInputs[playlist.id] || '', {
        sourceLabel: 'manual pasted refresh',
      }),
    );
    setStatus(`Applied refresh for ${playlist.name}`);
  };

  const setAutomation = (playlist, enabled) => {
    setItems(
      updatePlaylistRefreshAutomation(playlist.id, {
        enabled,
        cadence: playlist.refreshCadence,
        cooldownDays: playlist.refreshCooldownDays,
        providerRefreshLimit: playlist.providerRefreshLimit,
      }),
    );
  };

  const fetchProviderRefresh = async (playlist, { apply = false } = {}) => {
    setBusyPlaylistId(playlist.id);
    try {
      const result = await sourceFeedImportsAPI.previewSourceFeedImport({
        fetchProviderUrls: true,
        limit: playlist.providerRefreshLimit,
        providerAccessToken,
        sourceKind: 'auto',
        sourceText: playlist.source,
      });
      const content = buildPlaylistProviderRefreshContent(result);
      if (!content.trim()) {
        setStatus(`Provider refresh for ${playlist.name} returned no rows`);
        return;
      }

      setItems(
        apply
          ? applyPlaylistIntakeRefresh(playlist.id, content, {
              sourceLabel: `${result.provider || playlist.provider} provider refresh`,
            })
          : previewPlaylistIntakeRefresh(playlist.id, content),
      );
      setStatus(
        `${apply ? 'Applied' : 'Previewed'} ${result.suggestionCount || 0} provider rows for ${playlist.name} with ${result.networkRequestCount || 0} provider requests`,
      );
    } catch (error) {
      setStatus(`Provider refresh failed for ${playlist.name}: ${error.message}`);
    } finally {
      setBusyPlaylistId('');
    }
  };

  const runDueRefreshes = async () => {
    if (dueRefreshes.length === 0) {
      setStatus('No mirrored playlist refreshes are due');
      return;
    }

    let applied = 0;
    for (const playlist of dueRefreshes) {
      // Sequential execution keeps provider request bursts bounded.
      // eslint-disable-next-line no-await-in-loop
      await fetchProviderRefresh(playlist, { apply: true });
      applied += 1;
    }
    setStatus(`Ran ${applied} due mirrored playlist refreshes`);
  };

  const createSlskdPlaylist = async (playlist) => {
    const itemsToCreate = buildPlaylistCollectionItems(playlist);
    if (itemsToCreate.length === 0) {
      setStatus(`No matched rows are ready for ${playlist.name}`);
      return;
    }

    setBusyPlaylistId(playlist.id);
    try {
      const created = await collectionsAPI.createCollection({
        description: `Created from Playlist Intake source ${playlist.source}. Rows are planned playlist entries until resolved to local library content.`,
        title: playlist.name,
        type: 'Playlist',
      });
      const collectionId = created.data?.id;
      if (!collectionId) {
        throw new Error('Collections API did not return a collection id');
      }

      for (const item of itemsToCreate) {
        // Sequential writes preserve playlist order and avoid API bursts.
        // eslint-disable-next-line no-await-in-loop
        await collectionsAPI.addCollectionItem(collectionId, item);
      }
      setItems(markPlaylistCollectionCreated(playlist.id, collectionId));
      setStatus(
        `Created playlist collection for ${playlist.name} with ${itemsToCreate.length} planned rows`,
      );
    } catch (error) {
      setStatus(`Playlist creation failed for ${playlist.name}: ${error.message}`);
    } finally {
      setBusyPlaylistId('');
    }
  };

  return (
    <Segment
      className="playlist-intake"
      raised
    >
      <div className="playlist-intake-header">
        <Header as="h2">
          <Icon name="list alternate outline" />
          <Header.Content>
            Playlist Intake
            <Header.Subheader>
              Import playlist text for review before any provider or network activity.
            </Header.Subheader>
          </Header.Content>
        </Header>
      </div>

      <Form className="playlist-intake-form">
        <Form.Group widths="equal">
          <Form.Input
            aria-label="Playlist name"
            label="Name"
            onChange={(event) => setName(event.target.value)}
            placeholder="Road trip, label sampler, friend recs"
            value={name}
          />
          <Form.Input
            aria-label="Playlist source"
            label="Source"
            onChange={(event) => setSource(event.target.value)}
            placeholder="Local file name or provider URL"
            value={source}
          />
        </Form.Group>
        <Form.Field>
          <label>Playlist rows</label>
          <TextArea
            aria-label="Playlist rows"
            onChange={(event) => setContent(event.target.value)}
            placeholder="Artist - Title, one row per track, or simple CSV artist,title"
            rows={6}
            value={content}
          />
        </Form.Field>
        <Form.Field>
          <Checkbox
            aria-label="Mirror playlist for refresh review"
            checked={mirrorEnabled}
            label="Mirror for refresh review"
            onChange={(_event, data) => setMirrorEnabled(data.checked)}
          />
        </Form.Field>
        {mirrorEnabled && (
          <Form.Group widths="equal">
            <Form.Select
              aria-label="Refresh cadence"
              label="Refresh cadence"
              onChange={(_event, data) => setRefreshCadence(data.value)}
              options={playlistRefreshCadences.map((cadence) => ({
                key: cadence,
                text: cadence,
                value: cadence,
              }))}
              value={refreshCadence}
            />
          <Form.Input
            aria-label="Refresh cooldown days"
            label="Cooldown days"
              min={1}
              max={90}
              onChange={(event) => setRefreshCooldownDays(event.target.value)}
              type="number"
              value={refreshCooldownDays}
            />
          </Form.Group>
        )}
        {mirrorEnabled && (
          <Form.Input
            aria-label="Provider bearer token"
            label="Provider bearer token"
            onChange={(event) => setProviderAccessToken(event.target.value)}
            placeholder="Optional; used for provider-backed refresh previews that require a token"
            type="password"
            value={providerAccessToken}
          />
        )}
        <Popup
          content="Stage this playlist in browser-local review. This does not fetch provider URLs, search Soulseek, browse peers, download, or create slskdN playlists."
          position="top center"
          trigger={
            <Button
              aria-label="Import playlist for review"
              disabled={!content.trim()}
              onClick={importPlaylist}
              primary
              type="button"
            >
              <Icon name="plus" />
              Import Playlist
            </Button>
          }
        />
      </Form>

      <div className="playlist-intake-summary">
        <Label color="blue">
          Playlists
          <Label.Detail>{summary.total}</Label.Detail>
        </Label>
        <Label color="green">
          Tracks
          <Label.Detail>{summary.tracks}</Label.Detail>
        </Label>
        <Label color="orange">
          Unmatched
          <Label.Detail>{summary.unmatched}</Label.Detail>
        </Label>
        <Label color="teal">
          Mirrored
          <Label.Detail>{summary.mirrored}</Label.Detail>
        </Label>
        <Label color={dueRefreshes.length > 0 ? 'orange' : 'grey'}>
          Due Refreshes
          <Label.Detail>{dueRefreshes.length}</Label.Detail>
        </Label>
        <Popup
          content="Run every due mirrored playlist refresh with automation enabled. Provider refreshes are executed sequentially and update only browser-local playlist intake rows."
          position="top center"
          trigger={
            <Button
              aria-label="Run due mirrored playlist refreshes"
              disabled={dueRefreshes.length === 0 || Boolean(busyPlaylistId)}
              icon
              onClick={runDueRefreshes}
              size="small"
              type="button"
            >
              <Icon name="calendar check" />
              Run Due Refreshes
            </Button>
          }
        />
      </div>
      {status && <div className="playlist-intake-status">{status}</div>}

      <div className="playlist-intake-grid">
        {items.map((playlist) => (
          <Segment
            className="playlist-intake-card"
            key={playlist.id}
          >
            {(() => {
              const completion = buildPlaylistCompletionSummary(playlist);
              const organizationInput = getOrganizationInput(playlist);

              return (
                <>
                  <div className="playlist-intake-card-head">
                    <div>
                      <div className="playlist-intake-title">{playlist.name}</div>
                      <div className="playlist-intake-meta">
                        <Icon name="map signs" />
                        {playlist.source}
                        <span> - </span>
                        <Icon name="plug" />
                        {playlist.provider}
                        <span> - </span>
                        <Icon
                          name={playlist.mirrorEnabled ? 'sync alternate' : 'lock'}
                        />
                        {playlist.mirrorEnabled
                          ? 'Mirror review enabled'
                          : 'One-time import'}
                      </div>
                      <div className="playlist-intake-summary">
                        <Label basic>
                          <Icon name="check" />
                          Matched {completion.Matched}
                        </Label>
                        <Label basic>
                          <Icon name="question circle outline" />
                          Unmatched {completion.Unmatched}
                        </Label>
                        <Label basic>
                          <Icon name="ban" />
                          Rejected {completion.Rejected}
                        </Label>
                        <Label
                          basic
                          color={completion.Unmatched > 0 ? 'orange' : 'green'}
                        >
                          {completion.Unmatched > 0
                            ? 'Partial completion allowed'
                            : 'Ready for review'}
                        </Label>
                      </div>
                      <div className="playlist-intake-impact">
                        {playlist.refreshPreview}
                      </div>
                      <div className="playlist-intake-policy">
                        <Label basic>
                          <Icon name="clock outline" />
                          Refresh cadence {playlist.refreshCadence}
                        </Label>
                        <Label basic>
                          <Icon name="hourglass half" />
                          Cooldown {playlist.refreshCooldownDays}d
                        </Label>
                        <Label
                          basic
                          color={playlist.refreshAutomationEnabled ? 'green' : 'grey'}
                        >
                          <Icon
                            name={
                              playlist.refreshAutomationEnabled ? 'power' : 'power off'
                            }
                          />
                          Automation{' '}
                          {playlist.refreshAutomationEnabled ? 'enabled' : 'disabled'}
                        </Label>
                        {playlist.refreshNextRunAt && (
                          <Label basic>
                            <Icon name="calendar alternate outline" />
                            Next {playlist.refreshNextRunAt}
                          </Label>
                        )}
                        {playlist.refreshCollectionId && (
                          <Label
                            basic
                            color="green"
                          >
                            Collection {playlist.refreshCollectionId}
                          </Label>
                        )}
                      </div>
                    </div>
                    <Label color={playlist.mirrorEnabled ? 'teal' : 'grey'}>
                      {playlist.state}
                    </Label>
                  </div>
                  {playlist.refreshDiff && (
                    <div className="playlist-intake-summary">
                      <Label color="green">
                        Added
                        <Label.Detail>{playlist.refreshDiff.addedCount}</Label.Detail>
                      </Label>
                      <Label color="orange">
                        Removed
                        <Label.Detail>{playlist.refreshDiff.removedCount}</Label.Detail>
                      </Label>
                      <Label color="yellow">
                        Changed
                        <Label.Detail>{playlist.refreshDiff.changedCount}</Label.Detail>
                      </Label>
                      <Label color="blue">
                        Unchanged
                        <Label.Detail>{playlist.refreshDiff.unchangedCount}</Label.Detail>
                      </Label>
                    </div>
                  )}
                  {playlist.mirrorEnabled && (
                    <div className="playlist-intake-refresh">
                      <Form>
                        <Form.Field>
                          <label>Refresh rows</label>
                          <TextArea
                            aria-label={`Refresh rows for ${playlist.name}`}
                            onChange={(event) =>
                              setRefreshInput(playlist, event.target.value)
                            }
                            placeholder="Paste the latest playlist rows to preview added and removed tracks"
                            rows={3}
                            value={refreshInputs[playlist.id] || ''}
                          />
                        </Form.Field>
                        <Popup
                          content="Preview added and removed rows for this mirrored playlist. This does not fetch providers, search, browse, download, or mutate the playlist."
                          position="top center"
                          trigger={
                            <Button
                              aria-label={`Preview refresh for ${playlist.name}`}
                              disabled={!(refreshInputs[playlist.id] || '').trim()}
                              icon
                              onClick={() => previewRefresh(playlist)}
                              size="small"
                              type="button"
                            >
                              <Icon name="sync alternate" />
                              Preview Refresh
                            </Button>
                          }
                        />
                        <Popup
                          content="Apply these pasted refresh rows to the mirrored playlist intake state. This updates browser-local rows only and does not search, browse, or download."
                          position="top center"
                          trigger={
                            <Button
                              aria-label={`Apply refresh for ${playlist.name}`}
                              disabled={!(refreshInputs[playlist.id] || '').trim()}
                              icon
                              onClick={() => applyManualRefresh(playlist)}
                              size="small"
                              type="button"
                            >
                              <Icon name="save" />
                              Apply Refresh
                            </Button>
                          }
                        />
                        <Popup
                          content="Fetch this playlist source through configured source-feed providers and preview the resulting refresh diff. This may contact the provider, but does not search Soulseek, browse peers, or download."
                          position="top center"
                          trigger={
                            <Button
                              aria-label={`Fetch provider refresh for ${playlist.name}`}
                              disabled={Boolean(busyPlaylistId)}
                              icon
                              loading={busyPlaylistId === playlist.id}
                              onClick={() => fetchProviderRefresh(playlist)}
                              size="small"
                              type="button"
                            >
                              <Icon name="cloud download" />
                              Fetch Provider Refresh
                            </Button>
                          }
                        />
                        <Popup
                          content="Enable or disable scheduled provider refresh for this mirrored playlist. Enabled refreshes run only when this Playlist Intake page runs due refreshes."
                          position="top center"
                          trigger={
                            <Button
                              aria-label={`${playlist.refreshAutomationEnabled ? 'Disable' : 'Enable'} scheduled refresh for ${playlist.name}`}
                              icon
                              onClick={() =>
                                setAutomation(
                                  playlist,
                                  !playlist.refreshAutomationEnabled,
                                )
                              }
                              size="small"
                              type="button"
                            >
                              <Icon
                                name={
                                  playlist.refreshAutomationEnabled
                                    ? 'toggle on'
                                    : 'toggle off'
                                }
                              />
                              Scheduled Refresh
                            </Button>
                          }
                        />
                      </Form>
                    </div>
                  )}
                  <div className="playlist-intake-actions">
                    <Popup
                      content="Send every non-rejected row from this playlist to Discovery Inbox review. This does not search, browse, download, or create a slskdN playlist."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Send reviewable rows from ${playlist.name} to Discovery Inbox`}
                          icon
                          onClick={() => sendPlaylistRowsToInbox(playlist)}
                          size="small"
                          type="button"
                        >
                          <Icon name="inbox" />
                          Send Reviewable Rows
                        </Button>
                      }
                    />
                    <Popup
                      content="Preview the matched rows that will be used for a slskdN playlist Collection."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Preview slskdN playlist for ${playlist.name}`}
                          icon
                          onClick={() => previewSlskdPlaylist(playlist)}
                          size="small"
                          type="button"
                        >
                          <Icon name="file alternate outline" />
                          Preview Playlist
                        </Button>
                      }
                    />
                    <Popup
                      content="Create a slskdN Playlist collection from matched rows. This writes local collection metadata only and does not search, browse, or download."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Create slskdN playlist for ${playlist.name}`}
                          icon
                          loading={busyPlaylistId === playlist.id}
                          onClick={() => createSlskdPlaylist(playlist)}
                          size="small"
                          type="button"
                        >
                          <Icon name="save" />
                          Create Playlist
                        </Button>
                      }
                    />
                    {bulkSent[playlist.id] && (
                      <Label color="blue">
                        Sent
                        <Label.Detail>{bulkSent[playlist.id]}</Label.Detail>
                      </Label>
                    )}
                  </div>
                  {playlistPreviews[playlist.id] && (
                    <div className="playlist-intake-preview">
                      <div className="playlist-intake-meta">
                        {playlistPreviews[playlist.id].networkImpact}
                      </div>
                      <pre>{playlistPreviews[playlist.id].text}</pre>
                    </div>
                  )}
                  <Segment className="playlist-intake-organization">
                    <Header as="h4">
                      <Icon name="tags" />
                      Tag and Organization Preview
                      <Header.Subheader>
                        Dry-run tag, cover-art, ReplayGain, and path decisions
                        before any file write.
                      </Header.Subheader>
                    </Header>
                    <Form>
                      <Form.Group widths="equal">
                        <Form.Input
                          aria-label={`Organization album title for ${playlist.name}`}
                          label="Album title"
                          onChange={(event) =>
                            setOrganizationInput(
                              playlist,
                              'albumTitle',
                              event.target.value,
                            )
                          }
                          value={organizationInput.albumTitle}
                        />
                        <Form.Select
                          aria-label={`Organization path template for ${playlist.name}`}
                          label="Path template"
                          onChange={(_event, data) =>
                            setOrganizationInput(playlist, 'pathTemplate', data.value)
                          }
                          options={organizationPathTemplates}
                          value={organizationInput.pathTemplate}
                        />
                      </Form.Group>
                      <Form.Group widths="equal">
                        <Form.Select
                          aria-label={`Multi-artist tag policy for ${playlist.name}`}
                          label="Multi-artist"
                          onChange={(_event, data) =>
                            setOrganizationInput(
                              playlist,
                              'multiArtistPolicy',
                              data.value,
                            )
                          }
                          options={multiArtistTagPolicies}
                          value={organizationInput.multiArtistPolicy}
                        />
                        <Form.Select
                          aria-label={`Cover art policy for ${playlist.name}`}
                          label="Cover art"
                          onChange={(_event, data) =>
                            setOrganizationInput(playlist, 'coverArtPolicy', data.value)
                          }
                          options={coverArtPolicies}
                          value={organizationInput.coverArtPolicy}
                        />
                        <Form.Select
                          aria-label={`ReplayGain policy for ${playlist.name}`}
                          label="ReplayGain"
                          onChange={(_event, data) =>
                            setOrganizationInput(playlist, 'replayGainPolicy', data.value)
                          }
                          options={replayGainPolicies}
                          value={organizationInput.replayGainPolicy}
                        />
                      </Form.Group>
                      <Popup
                        content="Preview tag fields, cover-art policy, ReplayGain policy, and organized destination paths. This does not write tags, move files, run ReplayGain, or contact providers."
                        position="top center"
                        trigger={
                          <Button
                            aria-label={`Preview tag organization for ${playlist.name}`}
                            disabled={completion.Matched === 0}
                            icon
                            onClick={() => previewTagOrganization(playlist)}
                            size="small"
                            type="button"
                          >
                            <Icon name="magic" />
                            Preview Organization
                          </Button>
                        }
                      />
                      <Popup
                        content="Copy a text report of the current tag and organization dry run. This does not write tags, move files, run ReplayGain, or contact providers."
                        position="top center"
                        trigger={
                          <Button
                            aria-label={`Copy tag organization report for ${playlist.name}`}
                            disabled={!playlist.organizationPlan}
                            icon
                            onClick={() => copyTagOrganizationReport(playlist)}
                            size="small"
                            type="button"
                          >
                            <Icon name="copy outline" />
                            Copy Report
                          </Button>
                        }
                      />
                      <Popup
                        content="Approve this dry-run snapshot for later import review. This records source/proposed metadata only and does not write tags or move files."
                        position="top center"
                        trigger={
                          <Button
                            aria-label={`Approve tag organization snapshot for ${playlist.name}`}
                            disabled={!playlist.organizationPlan}
                            icon
                            onClick={() => approveTagOrganizationSnapshot(playlist)}
                            size="small"
                            type="button"
                          >
                            <Icon name="check circle outline" />
                            Approve Snapshot
                          </Button>
                        }
                      />
                      <Popup
                        content="Clear the approved snapshot while keeping the dry-run preview available for more edits."
                        position="top center"
                        trigger={
                          <Button
                            aria-label={`Clear tag organization snapshot for ${playlist.name}`}
                            disabled={!playlist.organizationApproval}
                            icon
                            onClick={() => clearTagOrganizationSnapshot(playlist)}
                            size="small"
                            type="button"
                          >
                            <Icon name="undo" />
                            Clear Snapshot
                          </Button>
                        }
                      />
                    </Form>
                    {playlist.organizationPlan && (
                      <div className="playlist-intake-organization-plan">
                        <div className="playlist-intake-summary">
                          <Label color="green">
                            Matched
                            <Label.Detail>
                              {playlist.organizationPlan.summary.matched}
                            </Label.Detail>
                          </Label>
                          <Label color="orange">
                            Skipped
                            <Label.Detail>
                              {playlist.organizationPlan.summary.skipped}
                            </Label.Detail>
                          </Label>
                          <Label color="blue">
                            Changed fields
                            <Label.Detail>
                              {playlist.organizationPlan.summary.changedFieldCount}
                            </Label.Detail>
                          </Label>
                        </div>
                        <div className="playlist-intake-impact">
                          {playlist.organizationPlan.networkImpact}
                        </div>
                        <div className="playlist-intake-impact">
                          {playlist.organizationPlan.coverArtAction}
                        </div>
                        <div className="playlist-intake-impact">
                          {playlist.organizationPlan.replayGainAction}
                        </div>
                        <div className="playlist-intake-organization-destinations">
                          {playlist.organizationPlan.trackPreviews.map((preview) => (
                            <div
                              className="playlist-intake-organization-row"
                              key={preview.trackId}
                            >
                              <div>
                                <strong>{preview.destinationPath}</strong>
                                <div className="playlist-intake-meta">
                                  {preview.changedFields.join(', ')}
                                </div>
                              </div>
                              <Label basic>
                                Line {preview.lineNumber}
                              </Label>
                            </div>
                          ))}
                        </div>
                        {playlist.organizationPlan.skippedTracks.length > 0 && (
                          <div className="playlist-intake-impact">
                            Skipped:{' '}
                            {playlist.organizationPlan.skippedTracks
                              .map((track) => track.title)
                              .join(', ')}
                          </div>
                        )}
                        {playlist.organizationApproval && (
                          <div className="playlist-intake-organization-approval">
                            <Label color="green">
                              <Icon name="check circle outline" />
                              Snapshot approved
                            </Label>
                            <span>
                              {playlist.organizationApproval.summary.matched} tracks,
                              {' '}
                              {
                                playlist.organizationApproval.summary
                                  .changedFieldCount
                              }{' '}
                              changed fields
                            </span>
                            <div className="playlist-intake-impact">
                              {playlist.organizationApproval.impact}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </Segment>
                  <div className="playlist-intake-tracks">
                    {playlist.tracks.map((track) => (
                      <div
                        className="playlist-intake-track"
                        key={track.id}
                      >
                        <div>
                          <strong>
                            {[track.artist, track.title].filter(Boolean).join(' - ') ||
                              track.title}
                          </strong>
                          <div className="playlist-intake-meta">
                            <Icon name="list ol" />
                            Line {track.lineNumber}
                            <span> - </span>
                            <Icon name="quote right" />
                            {track.sourceLine}
                          </div>
                        </div>
                        <div className="playlist-intake-track-actions">
                          <Label color={trackStateColor(track.state)}>
                            {track.state}
                          </Label>
                          <Popup
                            content="Mark this row as matched for partial playlist completion review. This does not search or download it."
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Mark ${track.title} matched`}
                                disabled={track.state === 'Matched'}
                                icon="check"
                                onClick={() => setTrackState(playlist, track, 'Matched')}
                                size="small"
                              />
                            }
                          />
                          <Popup
                            content="Mark this row unmatched so it can be reviewed or rematched later without losing source identity."
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Mark ${track.title} unmatched`}
                                disabled={track.state === 'Unmatched'}
                                icon="question circle outline"
                                onClick={() =>
                                  setTrackState(playlist, track, 'Unmatched')
                                }
                                size="small"
                              />
                            }
                          />
                          <Popup
                            content="Reject this row from playlist completion while keeping it in the imported source evidence."
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Reject playlist row ${track.title}`}
                                disabled={track.state === 'Rejected'}
                                icon="ban"
                                negative
                                onClick={() => setTrackState(playlist, track, 'Rejected')}
                                size="small"
                              />
                            }
                          />
                          <Popup
                            content="Send this playlist row to Discovery Inbox review. This does not start provider lookups, searches, peer browsing, or downloads."
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Send ${track.title} to Discovery Inbox`}
                                icon="inbox"
                                onClick={() => sendTrackToInbox(playlist, track)}
                                size="small"
                              />
                            }
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </>
              );
            })()}
          </Segment>
        ))}
      </div>
    </Segment>
  );
};

export default PlaylistIntake;
