import './ImportStaging.css';
import {
  audioVerificationProfiles,
  buildAudioVerificationDecision,
  clearAudioVerificationCache,
  verifyAudioFile,
} from '../../lib/audioVerification';
import {
  addImportStagingFiles,
  addImportStagingItemToDenylist,
  applyAudioVerificationPolicy,
  getImportStagingDenylist,
  getImportStagingItems,
  matchAllImportStagingItems,
  overrideImportStagingItemMetadataMatch,
  removeImportStagingDenylistEntry,
  updateImportStagingItemAudioVerification,
  updateImportStagingItemMetadataMatch,
  updateImportStagingItemState,
} from '../../lib/importStaging';
import React, { useMemo, useRef, useState } from 'react';
import {
  Button,
  Form,
  Header,
  Icon,
  Label,
  Popup,
  Segment,
  Table,
} from 'semantic-ui-react';

const stateColors = {
  Failed: 'red',
  Imported: 'teal',
  Ready: 'green',
  Rejected: 'red',
  Staged: 'violet',
};

const formatBytes = (bytes) => {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1,
  );
  const value = bytes / (1024 ** exponent);
  return `${value.toFixed(value >= 10 || exponent === 0 ? 0 : 1)} ${units[exponent]}`;
};

const formatDate = (timestamp) => {
  if (!timestamp) return 'Unknown';
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? 'Unknown' : date.toLocaleString();
};

const getMatchColor = (match) => {
  if (!match) return 'grey';
  if (match.confidence >= 0.85) return 'green';
  if (match.confidence >= 0.65) return 'yellow';
  return 'orange';
};

const renderMetadataMatch = (match) => {
  if (!match) {
    return (
      <Label color="grey">
        Unmatched
      </Label>
    );
  }

  return (
    <div>
      <Label color={getMatchColor(match)}>
        {match.status}
        <Label.Detail>{Math.round(match.confidence * 100)}%</Label.Detail>
      </Label>
      <div className="import-staging-meta">
        {[match.artist, match.album, match.title].filter(Boolean).join(' - ') ||
          'No parsed identity'}
      </div>
      <div className="import-staging-meta">
        Band {match.band || 'Review'} · Strongest: {match.strongestEvidence}
      </div>
      <div className="import-staging-meta">
        Weakest: {match.weakestEvidence}
      </div>
      {match.warnings.length > 0 && (
        <div className="import-staging-warning">
          {match.warnings.join(' ')}
        </div>
      )}
    </div>
  );
};

const renderFingerprintVerification = (fingerprint) => {
  if (!fingerprint) {
    return (
      <Label color="grey">
        Not Requested
      </Label>
    );
  }

  if (fingerprint.status !== 'Verified') {
    return (
      <Label color="orange">
        {fingerprint.status}
      </Label>
    );
  }

  return (
    <div>
      <Label color="green">
        Verified
        <Label.Detail>{fingerprint.algorithm.toUpperCase()}</Label.Detail>
      </Label>
      <div className="import-staging-fingerprint">
        {fingerprint.value.slice(0, 16)}
      </div>
    </div>
  );
};

const verificationColors = {
  Disabled: 'grey',
  Failed: 'red',
  Review: 'orange',
  Verified: 'green',
};

const renderAudioVerification = (verification) => {
  if (!verification) {
    return (
      <Label color="grey">
        Not Requested
      </Label>
    );
  }

  return (
    <div>
      <Label color={verificationColors[verification.status] || 'grey'}>
        {verification.status}
        <Label.Detail>{Math.round((verification.confidence || 0) * 100)}%</Label.Detail>
      </Label>
      <div className="import-staging-meta">
        {verification.profileId} · {verification.failMode} · {verification.action}
      </div>
      {verification.evidence?.length > 0 && (
        <div className="import-staging-meta">
          {verification.evidence.join(' ')}
        </div>
      )}
      {verification.warnings?.length > 0 && (
        <div className="import-staging-warning">
          {verification.warnings.join(' ')}
        </div>
      )}
    </div>
  );
};

const ImportStaging = () => {
  const fileInputRef = useRef(null);
  const [cacheVerification, setCacheVerification] = useState(true);
  const [fingerprintOnAdd, setFingerprintOnAdd] = useState(false);
  const [verificationProfile, setVerificationProfile] = useState('balanced');
  const [denylist, setDenylist] = useState(() => getImportStagingDenylist());
  const [items, setItems] = useState(() => getImportStagingItems());
  const [overrideInputs, setOverrideInputs] = useState({});

  const counts = useMemo(
    () =>
      items.reduce(
        (result, item) => ({
          ...result,
          [item.state]: (result[item.state] || 0) + 1,
        }),
        {},
      ),
    [items],
  );

  const selectFiles = () => {
    fileInputRef.current?.click();
  };

  const addFiles = async (event) => {
    const files = Array.from(event.target.files || []);
    const stagedFiles = fingerprintOnAdd
      ? await Promise.all(
          files.map(async (file) => {
            const result = await verifyAudioFile(file, {
              cacheEnabled: cacheVerification,
              profileId: verificationProfile,
            });

            return {
              audioVerification: result.verification,
              fingerprintVerification: result.fingerprint,
              lastModified: file.lastModified,
              name: file.name,
              size: file.size,
              type: file.type,
            };
          }),
        )
      : files;

    setItems(addImportStagingFiles(stagedFiles));
    event.target.value = '';
  };

  const setItemState = (item, state) => {
    setItems(updateImportStagingItemState(item.id, state));
  };

  const denyItem = (item) => {
    setItemState(item, 'Rejected');
    setDenylist(
      addImportStagingItemToDenylist(
        item.id,
        'Rejected from import staging review.',
      ),
    );
  };

  const removeDeniedEntry = (entry) => {
    setDenylist(removeImportStagingDenylistEntry(entry.key));
  };

  const matchItem = (item) => {
    setItems(updateImportStagingItemMetadataMatch(item.id));
  };

  const matchAll = () => {
    setItems(matchAllImportStagingItems());
  };

  const verifyItem = (item) => {
    const verification = buildAudioVerificationDecision({
      file: item,
      fingerprint: item.fingerprintVerification,
      profileId: verificationProfile,
    });

    setItems(updateImportStagingItemAudioVerification(item.id, verification));
  };

  const applyVerificationPolicyToItem = (item) => {
    setItems(
      updateImportStagingItemState(item.id, applyAudioVerificationPolicy(item)),
    );
  };

  const setOverrideInput = (item, field, value) => {
    setOverrideInputs((current) => ({
      ...current,
      [item.id]: {
        ...(current[item.id] || {}),
        [field]: value,
      },
    }));
  };

  const applyOverride = (item) => {
    const override = overrideInputs[item.id] || {};
    setItems(
      overrideImportStagingItemMetadataMatch(item.id, {
        album: override.album,
        artist: override.artist,
        title: override.title,
        trackNumber: Number.parseInt(override.trackNumber, 10) || null,
      }),
    );
  };

  return (
    <Segment
      className="import-staging"
      raised
    >
      <div className="import-staging-header">
        <Header as="h2">
          <Icon name="archive" />
          <Header.Content>
            Import Staging
            <Header.Subheader>
              Review local files before any library import or mutation.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <div className="import-staging-actions">
          <input
            className="import-staging-file-input"
            data-testid="import-staging-file-input"
            multiple
            onChange={addFiles}
            ref={fileInputRef}
            type="file"
          />
          <Popup
            content="Choose local files for staging review. This records browser-visible file metadata only and does not upload or import files."
            position="top center"
            trigger={
              <Button
                aria-label="Choose files for import staging"
                onClick={selectFiles}
                primary
              >
                <Icon name="folder open" />
                Choose Files
              </Button>
            }
          />
          <Popup
            content="Run local filename metadata matching for every staged row. This does not contact metadata services or modify files."
            position="top center"
            trigger={
              <Button
                aria-label="Match staged import metadata"
                disabled={items.length === 0}
                onClick={matchAll}
              >
                <Icon name="tags" />
                Match Metadata
              </Button>
            }
          />
          <Popup
            content="When enabled, newly selected files are hashed locally with SHA-256 in the browser. This reads the selected file bytes but does not upload or import them."
            position="top center"
            trigger={
              <label className="import-staging-fingerprint-toggle">
                <input
                  aria-label="Fingerprint on add"
                  checked={fingerprintOnAdd}
                  data-testid="import-staging-fingerprint-toggle"
                  onChange={(event) => setFingerprintOnAdd(event.target.checked)}
                  type="checkbox"
                />
                <span>Fingerprint on add</span>
              </label>
            }
          />
          <Form.Select
            aria-label="Audio verification profile"
            compact
            onChange={(_event, data) => setVerificationProfile(data.value)}
            options={audioVerificationProfiles.map((profile) => ({
              key: profile.id,
              text: `${profile.title} (${profile.failMode})`,
              value: profile.id,
            }))}
            value={verificationProfile}
          />
          <Popup
            content="Cache browser-computed fingerprints so repeated verification of the same local file metadata can reuse the previous hash."
            position="top center"
            trigger={
              <label className="import-staging-fingerprint-toggle">
                <input
                  aria-label="Cache verification fingerprints"
                  checked={cacheVerification}
                  onChange={(event) => setCacheVerification(event.target.checked)}
                  type="checkbox"
                />
                <span>Cache verification</span>
              </label>
            }
          />
          <Popup
            content="Clear the browser-local audio verification fingerprint cache."
            position="top center"
            trigger={
              <Button
                aria-label="Clear audio verification cache"
                onClick={clearAudioVerificationCache}
              >
                <Icon name="eraser" />
                Clear Verification Cache
              </Button>
            }
          />
        </div>
      </div>

      <div className="import-staging-summary">
        {['Staged', 'Ready', 'Imported', 'Rejected', 'Failed'].map((state) => (
          <Label
            color={stateColors[state]}
            key={state}
          >
            {state}
            <Label.Detail>{counts[state] || 0}</Label.Detail>
          </Label>
        ))}
        <Label color="red">
          Denylist
          <Label.Detail>{denylist.length}</Label.Detail>
        </Label>
      </div>

      {denylist.length > 0 && (
        <Segment className="import-staging-denylist">
          <Header as="h3">
            <Icon name="ban" />
            Failed Import Denylist
          </Header>
          <div className="import-staging-denylist-grid">
            {denylist.map((entry) => (
              <div
                className="import-staging-denylist-entry"
                key={entry.key}
              >
                <div>
                  <strong>{entry.fileName}</strong>
                  <div className="import-staging-meta">
                    {entry.reason}
                  </div>
                </div>
                <Popup
                  content="Remove this denylist entry so matching staged files can be reviewed normally again."
                  position="top center"
                  trigger={
                    <Button
                      aria-label={`Remove ${entry.fileName} from failed import denylist`}
                      compact
                      icon="trash"
                      onClick={() => removeDeniedEntry(entry)}
                      size="tiny"
                    />
                  }
                />
              </div>
            ))}
          </div>
        </Segment>
      )}

      {items.length === 0 ? (
        <Segment
          className="import-staging-empty"
          placeholder
        >
          <Header icon>
            <Icon name="archive" />
            No staged imports yet
          </Header>
          <p>
            Choose files to build a review queue before library mutation is wired.
          </p>
        </Segment>
      ) : (
        <Table
          className="import-staging-table"
          celled
          striped
        >
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>File</Table.HeaderCell>
              <Table.HeaderCell width={2}>Size</Table.HeaderCell>
              <Table.HeaderCell width={4}>Metadata Match</Table.HeaderCell>
              <Table.HeaderCell width={3}>Fingerprint</Table.HeaderCell>
              <Table.HeaderCell width={3}>Audio Verification</Table.HeaderCell>
              <Table.HeaderCell width={2}>State</Table.HeaderCell>
              <Table.HeaderCell width={4}>Actions</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {items.map((item) => (
              <Table.Row key={item.id}>
                <Table.Cell data-label="File">
                  <div className="import-staging-file">{item.fileName}</div>
                  <div className="import-staging-meta">
                    {item.type || 'Unknown type'} · Modified {formatDate(item.lastModified)}
                  </div>
                </Table.Cell>
                <Table.Cell data-label="Size">{formatBytes(item.size)}</Table.Cell>
                <Table.Cell data-label="Metadata Match">
                  {renderMetadataMatch(item.metadataMatch)}
                  <Form className="import-staging-override-form">
                    <Form.Group widths="equal">
                      <Form.Input
                        aria-label={`Override artist for ${item.fileName}`}
                        onChange={(event) =>
                          setOverrideInput(item, 'artist', event.target.value)
                        }
                        placeholder="Artist"
                        value={overrideInputs[item.id]?.artist || ''}
                      />
                      <Form.Input
                        aria-label={`Override title for ${item.fileName}`}
                        onChange={(event) =>
                          setOverrideInput(item, 'title', event.target.value)
                        }
                        placeholder="Title"
                        value={overrideInputs[item.id]?.title || ''}
                      />
                    </Form.Group>
                  </Form>
                </Table.Cell>
                <Table.Cell data-label="Fingerprint">
                  {renderFingerprintVerification(item.fingerprintVerification)}
                </Table.Cell>
                <Table.Cell data-label="Audio Verification">
                  {renderAudioVerification(item.audioVerification)}
                </Table.Cell>
                <Table.Cell data-label="State">
                  <Label color={stateColors[item.state]}>{item.state}</Label>
                </Table.Cell>
                <Table.Cell data-label="Actions">
                  <div className="import-staging-row-actions">
                    <Popup
                      content="Parse this filename into a local metadata confidence result. This does not contact MusicBrainz or fingerprint the file."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Match metadata for ${item.fileName}`}
                          compact
                          onClick={() => matchItem(item)}
                          size="tiny"
                        >
                          <Icon name="tags" />
                          Match
                        </Button>
                      }
                    />
                    <Popup
                      content="Apply the manual artist/title override as the accepted metadata match for this staged file."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Apply metadata override for ${item.fileName}`}
                          compact
                          disabled={
                            !overrideInputs[item.id]?.artist &&
                            !overrideInputs[item.id]?.title
                          }
                          onClick={() => applyOverride(item)}
                          size="tiny"
                        >
                          <Icon name="edit" />
                          Override
                        </Button>
                      }
                    />
                    <Popup
                      content="Run audio verification for this staged item using the selected profile. This reads only browser-accessible file metadata or a staged hash and does not import or move files."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Verify audio for ${item.fileName}`}
                          compact
                          onClick={() => verifyItem(item)}
                          size="tiny"
                        >
                          <Icon name="shield" />
                          Verify
                        </Button>
                      }
                    />
                    <Popup
                      content="Apply the selected audio verification profile policy to this row. Verified files become Ready, fail-closed quarantine results become Failed, and review results stay Staged for manual review."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Apply audio verification policy for ${item.fileName}`}
                          compact
                          disabled={!item.audioVerification}
                          onClick={() => applyVerificationPolicyToItem(item)}
                          size="tiny"
                        >
                          <Icon name="balance scale" />
                          Policy
                        </Button>
                      }
                    />
                    <Popup
                      content="Mark this staged file ready for the later import step. This does not move or modify the file."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Mark ${item.fileName} ready`}
                          compact
                          disabled={item.state === 'Ready'}
                          onClick={() => setItemState(item, 'Ready')}
                          positive
                          size="tiny"
                        >
                          <Icon name="check" />
                          Ready
                        </Button>
                      }
                    />
                    <Popup
                      content="Mark this staged file as imported after you have verified the library outcome."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Mark ${item.fileName} imported`}
                          compact
                          disabled={item.state === 'Imported'}
                          onClick={() => setItemState(item, 'Imported')}
                          size="tiny"
                        >
                          <Icon name="download" />
                          Imported
                        </Button>
                      }
                    />
                    <Popup
                      content="Reject this staged file so it stays visible as a reviewed non-import candidate."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Reject ${item.fileName}`}
                          compact
                          disabled={item.state === 'Rejected'}
                          negative
                          onClick={() => denyItem(item)}
                          size="tiny"
                        >
                          <Icon name="ban" />
                          Reject
                        </Button>
                      }
                    />
                  </div>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </Segment>
  );
};

export default ImportStaging;
