// <copyright file="RealmSubjectIndexConflicts.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import {
  fetchRealmSubjectIndexConflicts,
  formatRealmSubjectIndexConflictReport,
  summarizeRealmSubjectIndexConflicts,
} from '../../../lib/realmSubjectIndexes';
import React, { useMemo, useState } from 'react';
import {
  Button,
  Header,
  Icon,
  Input,
  Label,
  Message,
  Popup,
  Segment,
  Statistic,
  Table,
} from 'semantic-ui-react';

const get = (source, camel, pascal, fallback = '') =>
  source?.[camel] ?? source?.[pascal] ?? fallback;

const RealmSubjectIndexConflicts = () => {
  const [copyStatus, setCopyStatus] = useState('');
  const [disabledAuthorities, setDisabledAuthorities] = useState([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [realmId, setRealmId] = useState('scene-realm');
  const [report, setReport] = useState(null);
  const conflicts = report?.conflicts || report?.Conflicts || [];
  const summary = useMemo(
    () => summarizeRealmSubjectIndexConflicts(report || {}),
    [report],
  );

  const loadConflicts = async () => {
    const trimmedRealmId = realmId.trim();
    if (!trimmedRealmId) {
      setError('Enter a realm id before loading subject-index conflicts.');
      return;
    }

    setCopyStatus('');
    setError('');
    setLoading(true);
    try {
      const response = await fetchRealmSubjectIndexConflicts({
        realmId: trimmedRealmId,
      });
      setReport(response.data);
    } catch (loadError) {
      setError(
        loadError?.response?.data ||
          loadError?.response?.statusText ||
          loadError?.message ||
          'Unable to load realm subject-index conflicts.',
      );
    } finally {
      setLoading(false);
    }
  };

  const copyReport = async () => {
    if (!report) return;

    const text = formatRealmSubjectIndexConflictReport({
      disabledAuthorities,
      report,
    });

    if (!navigator.clipboard?.writeText) {
      setCopyStatus('Clipboard unavailable; copy the visible conflict report manually.');
      return;
    }

    try {
      await navigator.clipboard.writeText(text);
      setCopyStatus('Realm subject-index conflict report copied.');
    } catch {
      setCopyStatus('Unable to copy realm subject-index conflict report.');
    }
  };

  const toggleAuthority = (authorityKey) => {
    setDisabledAuthorities((current) =>
      current.includes(authorityKey)
        ? current.filter((key) => key !== authorityKey)
        : [...current, authorityKey],
    );
  };

  return (
    <Segment className="realm-subject-index-conflicts">
      <div className="mesh-evidence-policy-header">
        <Header as="h3">
          <Icon name="object group" />
          <Header.Content>
            Realm Subject Index Conflicts
            <Header.Subheader>
              Review accepted realm index disagreements with provenance before trusting a realm authority.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <div className="integration-actions">
          <Popup
            content="Load the read-only conflict report for this realm. This does not publish, disable, search, browse, or download."
            position="top center"
            trigger={
              <Button
                aria-label="Load realm subject-index conflicts"
                loading={loading}
                onClick={loadConflicts}
                primary
              >
                <Icon name="refresh" />
                Load Conflicts
              </Button>
            }
          />
          <Popup
            content="Copy the visible conflict report, including local authority disable selections. This does not change backend realm governance."
            position="top center"
            trigger={
              <Button
                aria-label="Copy realm subject-index conflict report"
                disabled={!report}
                onClick={copyReport}
              >
                <Icon name="copy" />
                Copy Report
              </Button>
            }
          />
        </div>
      </div>
      <Input
        aria-label="Realm subject-index realm id"
        fluid
        label="Realm ID"
        onChange={(_, { value }) => setRealmId(value)}
        placeholder="scene-realm"
        value={realmId}
      />
      {error && (
        <Message
          error
          size="small"
        >
          {String(error)}
        </Message>
      )}
      {copyStatus && (
        <Message
          info
          size="small"
        >
          {copyStatus}
        </Message>
      )}
      {report && (
        <>
          <Statistic.Group
            size="small"
            widths="five"
          >
            <Statistic>
              <Statistic.Value>{summary.indexCount}</Statistic.Value>
              <Statistic.Label>Indexes</Statistic.Label>
            </Statistic>
            <Statistic>
              <Statistic.Value>{summary.entryCount}</Statistic.Value>
              <Statistic.Label>Entries</Statistic.Label>
            </Statistic>
            <Statistic color={summary.conflictCount > 0 ? 'orange' : 'green'}>
              <Statistic.Value>{summary.conflictCount}</Statistic.Value>
              <Statistic.Label>Conflicts</Statistic.Label>
            </Statistic>
            <Statistic>
              <Statistic.Value>{summary.conflictTypeCount}</Statistic.Value>
              <Statistic.Label>Types</Statistic.Label>
            </Statistic>
            <Statistic color={disabledAuthorities.length > 0 ? 'yellow' : 'grey'}>
              <Statistic.Value>{disabledAuthorities.length}</Statistic.Value>
              <Statistic.Label>Local Disables</Statistic.Label>
            </Statistic>
          </Statistic.Group>
          {conflicts.length === 0 ? (
            <Message
              positive
              size="small"
            >
              No subject-index conflicts reported for {get(report, 'realmId', 'RealmId')}.
            </Message>
          ) : (
            <Table
              celled
              compact
              stackable
            >
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>Conflict</Table.HeaderCell>
                  <Table.HeaderCell>Subject</Table.HeaderCell>
                  <Table.HeaderCell>Values</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {conflicts.map((conflict) => (
                  <Table.Row key={get(conflict, 'id', 'Id', get(conflict, 'key', 'Key'))}>
                    <Table.Cell>
                      <Label color="orange">
                        {get(conflict, 'type', 'Type', 'unknown')}
                      </Label>
                      <div>
                        <strong>{get(conflict, 'key', 'Key', '-')}</strong>
                      </div>
                      <div className="integration-muted-copy">
                        {get(conflict, 'description', 'Description', '-')}
                      </div>
                    </Table.Cell>
                    <Table.Cell>
                      <div>{get(conflict, 'subjectId', 'SubjectId', '-')}</div>
                      <div className="integration-muted-copy">
                        {get(conflict, 'subjectNamespace', 'SubjectNamespace', '-')}
                      </div>
                    </Table.Cell>
                    <Table.Cell>
                      {(conflict.values || conflict.Values || []).map((value) => {
                        const authorityKey = get(
                          value,
                          'authorityKey',
                          'AuthorityKey',
                          '-',
                        );
                        const disabled = disabledAuthorities.includes(authorityKey);

                        return (
                          <Segment
                            compact
                            key={`${authorityKey}:${get(value, 'value', 'Value')}`}
                          >
                            <div className="integration-status-row">
                              <Label color={disabled ? 'grey' : 'green'}>
                                {disabled ? 'Locally disabled' : 'Active'}
                              </Label>
                              <Label basic>{authorityKey}</Label>
                              <Popup
                                content={`${disabled ? 'Re-enable' : 'Disable'} this authority locally for review. This only changes browser review state and does not update realm governance.`}
                                position="top center"
                                trigger={
                                  <Button
                                    aria-label={`${disabled ? 'Enable' : 'Disable'} authority ${authorityKey}`}
                                    onClick={() => toggleAuthority(authorityKey)}
                                    size="mini"
                                  >
                                    <Icon name={disabled ? 'unlock' : 'ban'} />
                                    {disabled ? 'Enable' : 'Disable'}
                                  </Button>
                                }
                              />
                            </div>
                            <div>{get(value, 'value', 'Value', '-')}</div>
                            <div className="integration-muted-copy">
                              {get(value, 'provenance', 'Provenance', '-')}
                            </div>
                          </Segment>
                        );
                      })}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </>
      )}
    </Segment>
  );
};

export default RealmSubjectIndexConflicts;
