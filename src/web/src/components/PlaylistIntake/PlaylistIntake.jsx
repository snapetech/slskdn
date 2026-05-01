import './PlaylistIntake.css';
import * as collectionsAPI from '../../lib/collections';
import {
  addPlaylistIntake,
  approvePlaylistTagOrganizationPlan,
  buildPlaylistCollectionItems,
  buildPlaylistCompletionSummary,
  buildPlaylistIntakeSummary,
  buildSlskdPlaylistPreview,
  clearPlaylistTagOrganizationApproval,
  coverArtPolicies,
  formatPlaylistTagOrganizationReport,
  getPlaylistIntakes,
  markPlaylistCollectionCreated,
  multiArtistTagPolicies,
  organizationPathTemplates,
  previewPlaylistTagOrganizationPlan,
  replayGainPolicies,
  updatePlaylistIntakeTrackState,
} from '../../lib/playlistIntake';
import React, { useMemo, useState } from 'react';
import {
  Button,
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
  const [playlistPreviews, setPlaylistPreviews] = useState({});
  const [organizationInputs, setOrganizationInputs] = useState({});
  const [status, setStatus] = useState('');
  const [busyPlaylistId, setBusyPlaylistId] = useState('');

  const summary = useMemo(() => buildPlaylistIntakeSummary(items), [items]);

  const importPlaylist = () => {
    const sourceName = source.trim() || name.trim() || 'Pasted playlist';
    const nextItems = addPlaylistIntake({
      content,
      name: name.trim() || sourceName,
      source: sourceName,
    });

    setItems(nextItems);
    setName('');
    setSource('');
    setContent('');
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
        <Popup
          content="Import these playlist rows for matching and playlist creation. This does not search Soulseek, browse peers, download, or track provider playlists."
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
                    </div>
                    <Label color="grey">
                      {playlist.state}
                    </Label>
                  </div>
                  <div className="playlist-intake-actions">
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
