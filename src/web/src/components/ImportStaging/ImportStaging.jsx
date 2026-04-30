import './ImportStaging.css';
import { fingerprintFile } from '../../lib/fileFingerprint';
import {
  addImportStagingFiles,
  getImportStagingItems,
  matchAllImportStagingItems,
  updateImportStagingItemMetadataMatch,
  updateImportStagingItemState,
} from '../../lib/importStaging';
import React, { useMemo, useRef, useState } from 'react';
import {
  Button,
  Checkbox,
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

const ImportStaging = () => {
  const fileInputRef = useRef(null);
  const [fingerprintOnAdd, setFingerprintOnAdd] = useState(false);
  const [items, setItems] = useState(() => getImportStagingItems());

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
          files.map(async (file) => ({
            fingerprintVerification: await fingerprintFile(file),
            lastModified: file.lastModified,
            name: file.name,
            size: file.size,
            type: file.type,
          })),
        )
      : files;

    setItems(addImportStagingFiles(stagedFiles));
    event.target.value = '';
  };

  const setItemState = (item, state) => {
    setItems(updateImportStagingItemState(item.id, state));
  };

  const matchItem = (item) => {
    setItems(updateImportStagingItemMetadataMatch(item.id));
  };

  const matchAll = () => {
    setItems(matchAllImportStagingItems());
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
              <Checkbox
                aria-label="Fingerprint on add"
                checked={fingerprintOnAdd}
                data-testid="import-staging-fingerprint-toggle"
                label="Fingerprint on add"
                onChange={(_, data) => setFingerprintOnAdd(data.checked)}
                toggle
              />
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
      </div>

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
          celled
          striped
        >
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>File</Table.HeaderCell>
              <Table.HeaderCell width={2}>Size</Table.HeaderCell>
              <Table.HeaderCell width={4}>Metadata Match</Table.HeaderCell>
              <Table.HeaderCell width={3}>Fingerprint</Table.HeaderCell>
              <Table.HeaderCell width={2}>State</Table.HeaderCell>
              <Table.HeaderCell width={4}>Actions</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {items.map((item) => (
              <Table.Row key={item.id}>
                <Table.Cell>
                  <div className="import-staging-file">{item.fileName}</div>
                  <div className="import-staging-meta">
                    {item.type || 'Unknown type'} · Modified {formatDate(item.lastModified)}
                  </div>
                </Table.Cell>
                <Table.Cell>{formatBytes(item.size)}</Table.Cell>
                <Table.Cell>{renderMetadataMatch(item.metadataMatch)}</Table.Cell>
                <Table.Cell>
                  {renderFingerprintVerification(item.fingerprintVerification)}
                </Table.Cell>
                <Table.Cell>
                  <Label color={stateColors[item.state]}>{item.state}</Label>
                </Table.Cell>
                <Table.Cell>
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
                          onClick={() => setItemState(item, 'Rejected')}
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
