import React, { useState, useEffect } from 'react';
import {
  Segment,
  Header,
  Button,
  Input,
  Table,
  Label,
  Icon,
  Message,
  Grid,
  Statistic,
  Tab,
  Loader,
} from 'semantic-ui-react';
import * as libraryHealth from '../../lib/libraryHealth';
import { LoaderSegment } from '../Shared';

const LibraryHealth = () => {
  const [libraryPath, setLibraryPath] = useState('');
  const [scanning, setScanning] = useState(false);
  const [summary, setSummary] = useState(null);
  const [issuesByType, setIssuesByType] = useState([]);
  const [issuesByArtist, setIssuesByArtist] = useState([]);
  const [issues, setIssues] = useState([]);
  const [selectedIssues, setSelectedIssues] = useState(new Set());
  const [fixing, setFixing] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const loadSummary = async (path) => {
    if (!path) return;
    
    try {
      setLoading(true);
      setError(null);
      const [summaryResp, byTypeResp, byArtistResp, issuesResp] = await Promise.all([
        libraryHealth.getSummary(path),
        libraryHealth.getIssuesByType(path),
        libraryHealth.getIssuesByArtist(10),
        libraryHealth.getIssues({ libraryPath: path, limit: 100 }),
      ]);
      setSummary(summaryResp.data);
      setIssuesByType(byTypeResp.data.groups || []);
      setIssuesByArtist(byArtistResp.data.groups || []);
      setIssues(issuesResp.data.issues || []);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load library health data');
    } finally {
      setLoading(false);
    }
  };

  const handleStartScan = async () => {
    if (!libraryPath) {
      setError('Please enter a library path');
      return;
    }

    try {
      setScanning(true);
      setError(null);
      const response = await libraryHealth.startScan(libraryPath);
      const scanId = response.data.scanId;

      // Poll for completion
      const poll = setInterval(async () => {
        const statusResp = await libraryHealth.getScanStatus(scanId);
        if (statusResp.data.status === 'Completed' || statusResp.data.status === 'Failed') {
          clearInterval(poll);
          setScanning(false);
          loadSummary(libraryPath);
        }
      }, 2000);

      setTimeout(() => {
        clearInterval(poll);
        setScanning(false);
        loadSummary(libraryPath);
      }, 60000); // Max 1 minute polling
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to start scan');
      setScanning(false);
    }
  };

  const getSeverityColor = (severity) => {
    switch (severity) {
      case 'Critical': return 'red';
      case 'High': return 'orange';
      case 'Medium': return 'yellow';
      case 'Low': return 'blue';
      case 'Info': return 'grey';
      default: return 'grey';
    }
  };

  const getIssueTypeLabel = (type) => {
    switch (type) {
      case 'SuspectedTranscode': return 'Suspected Transcode';
      case 'NonCanonicalVariant': return 'Non-Canonical Variant';
      case 'TrackNotInTaggedRelease': return 'Track Not in Tagged Release';
      case 'MissingTrackInRelease': return 'Missing Track in Release';
      case 'CorruptedFile': return 'Corrupted File';
      case 'MissingMetadata': return 'Missing Metadata';
      case 'MultipleVariants': return 'Multiple Variants';
      case 'WrongDuration': return 'Wrong Duration';
      default: return type;
    }
  };

  const handleToggleIssue = (issueId) => {
    const newSelected = new Set(selectedIssues);
    if (newSelected.has(issueId)) {
      newSelected.delete(issueId);
    } else {
      newSelected.add(issueId);
    }
    setSelectedIssues(newSelected);
  };

  const handleToggleAll = () => {
    if (selectedIssues.size === issues.length) {
      setSelectedIssues(new Set());
    } else {
      setSelectedIssues(new Set(issues.map(i => i.issueId)));
    }
  };

  const handleFixSelected = async () => {
    if (selectedIssues.size === 0) {
      setError('Please select issues to fix');
      return;
    }

    try {
      setFixing(true);
      setError(null);
      const issueIds = Array.from(selectedIssues);
      await libraryHealth.createRemediationJob(issueIds);
      setSelectedIssues(new Set());
      // Reload issues after a delay
      setTimeout(() => {
        loadSummary(libraryPath);
      }, 1000);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to create fix job');
    } finally {
      setFixing(false);
    }
  };

  const handleFixSingle = async (issueId) => {
    try {
      setFixing(true);
      setError(null);
      await libraryHealth.createRemediationJob([issueId]);
      setTimeout(() => {
        loadSummary(libraryPath);
      }, 1000);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to create fix job');
    } finally {
      setFixing(false);
    }
  };

  const OverviewPane = () => (
    <Tab.Pane>
      <Grid>
        <Grid.Row>
          <Grid.Column width={16}>
            <Segment>
              <Header as="h3">
                <Icon name="heartbeat" />
                <Header.Content>
                  Library Health Scanner
                  <Header.Subheader>Detect quality issues, transcodes, and missing tracks</Header.Subheader>
                </Header.Content>
              </Header>
            </Segment>
          </Grid.Column>
        </Grid.Row>

        <Grid.Row>
          <Grid.Column width={16}>
            <Segment>
              <Input
                fluid
                action={
                  <Button
                    primary
                    onClick={handleStartScan}
                    disabled={scanning || !libraryPath}
                    loading={scanning}
                  >
                    <Icon name="search" />
                    {scanning ? 'Scanning...' : 'Start Scan'}
                  </Button>
                }
                placeholder="Enter library path (e.g., /music or C:\Music)"
                value={libraryPath}
                onChange={(e) => setLibraryPath(e.target.value)}
                disabled={scanning}
              />
            </Segment>
          </Grid.Column>
        </Grid.Row>

        {error && (
          <Grid.Row>
            <Grid.Column width={16}>
              <Message negative>
                <Icon name="warning circle" />
                {error}
              </Message>
            </Grid.Column>
          </Grid.Row>
        )}

        {loading ? (
          <Grid.Row>
            <Grid.Column width={16}>
              <LoaderSegment>Loading library health data...</LoaderSegment>
            </Grid.Column>
          </Grid.Row>
        ) : summary ? (
          <>
            <Grid.Row>
              <Grid.Column width={16}>
                <Segment>
                  <Statistic.Group widths="three">
                    <Statistic>
                      <Statistic.Value>{summary.totalIssues}</Statistic.Value>
                      <Statistic.Label>Total Issues</Statistic.Label>
                    </Statistic>
                    <Statistic color="red">
                      <Statistic.Value>{summary.issuesOpen}</Statistic.Value>
                      <Statistic.Label>Open</Statistic.Label>
                    </Statistic>
                    <Statistic color="green">
                      <Statistic.Value>{summary.issuesResolved}</Statistic.Value>
                      <Statistic.Label>Resolved</Statistic.Label>
                    </Statistic>
                  </Statistic.Group>
                </Segment>
              </Grid.Column>
            </Grid.Row>

            <Grid.Row>
              <Grid.Column width={8}>
                <Segment>
                  <Header as="h4">Issues by Type</Header>
                  <Table compact>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Type</Table.HeaderCell>
                        <Table.HeaderCell textAlign="right">Count</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {issuesByType.length === 0 ? (
                        <Table.Row>
                          <Table.Cell colSpan={2} textAlign="center">
                            No issues detected
                          </Table.Cell>
                        </Table.Row>
                      ) : (
                        issuesByType.map((group) => (
                          <Table.Row key={group.type}>
                            <Table.Cell>
                              <Label basic>{getIssueTypeLabel(group.type)}</Label>
                            </Table.Cell>
                            <Table.Cell textAlign="right">
                              <strong>{group.count}</strong>
                            </Table.Cell>
                          </Table.Row>
                        ))
                      )}
                    </Table.Body>
                  </Table>
                </Segment>
              </Grid.Column>

              <Grid.Column width={8}>
                <Segment>
                  <Header as="h4">Top Artists with Issues</Header>
                  <Table compact>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Artist</Table.HeaderCell>
                        <Table.HeaderCell textAlign="right">Issues</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {issuesByArtist.length === 0 ? (
                        <Table.Row>
                          <Table.Cell colSpan={2} textAlign="center">
                            No artist data available
                          </Table.Cell>
                        </Table.Row>
                      ) : (
                        issuesByArtist.map((group, idx) => (
                          <Table.Row key={idx}>
                            <Table.Cell>{group.artist}</Table.Cell>
                            <Table.Cell textAlign="right">
                              <strong>{group.count}</strong>
                            </Table.Cell>
                          </Table.Row>
                        ))
                      )}
                    </Table.Body>
                  </Table>
                </Segment>
              </Grid.Column>
            </Grid.Row>
          </>
        ) : (
          <Grid.Row>
            <Grid.Column width={16}>
              <Segment placeholder>
                <Header icon>
                  <Icon name="search" />
                  Enter a library path and start a scan to detect issues
                </Header>
              </Segment>
            </Grid.Column>
          </Grid.Row>
        )}
      </Grid>
    </Tab.Pane>
  );

  const IssuesPane = () => (
    <Tab.Pane>
      <Grid>
        <Grid.Row>
          <Grid.Column width={16}>
            {error && (
              <Message negative>
                <Icon name="warning circle" />
                {error}
              </Message>
            )}
            
            {selectedIssues.size > 0 && (
              <Segment>
                <Button
                  primary
                  onClick={handleFixSelected}
                  disabled={fixing}
                  loading={fixing}
                >
                  <Icon name="wrench" />
                  Fix {selectedIssues.size} Selected Issue{selectedIssues.size > 1 ? 's' : ''}
                </Button>
                <Button
                  basic
                  onClick={() => setSelectedIssues(new Set())}
                  disabled={fixing}
                >
                  Clear Selection
                </Button>
              </Segment>
            )}

            {loading ? (
              <LoaderSegment>Loading issues...</LoaderSegment>
            ) : issues.length === 0 ? (
              <Segment placeholder>
                <Header icon>
                  <Icon name="check circle" color="green" />
                  No issues detected
                </Header>
              </Segment>
            ) : (
              <Table selectable celled>
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell collapsing>
                      <input
                        type="checkbox"
                        checked={selectedIssues.size === issues.length && issues.length > 0}
                        onChange={handleToggleAll}
                      />
                    </Table.HeaderCell>
                    <Table.HeaderCell>Type</Table.HeaderCell>
                    <Table.HeaderCell>Severity</Table.HeaderCell>
                    <Table.HeaderCell>Artist</Table.HeaderCell>
                    <Table.HeaderCell>Track</Table.HeaderCell>
                    <Table.HeaderCell>Reason</Table.HeaderCell>
                    <Table.HeaderCell>Status</Table.HeaderCell>
                    <Table.HeaderCell textAlign="center">Actions</Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {issues.map((issue) => (
                    <Table.Row key={issue.issueId}>
                      <Table.Cell collapsing>
                        <input
                          type="checkbox"
                          checked={selectedIssues.has(issue.issueId)}
                          onChange={() => handleToggleIssue(issue.issueId)}
                          disabled={!issue.canAutoFix}
                        />
                      </Table.Cell>
                      <Table.Cell>
                        <Label basic size="small">
                          {getIssueTypeLabel(issue.type)}
                        </Label>
                      </Table.Cell>
                      <Table.Cell>
                        <Label color={getSeverityColor(issue.severity)} size="small">
                          {issue.severity}
                        </Label>
                      </Table.Cell>
                      <Table.Cell>{issue.artist || '-'}</Table.Cell>
                      <Table.Cell>{issue.title || '-'}</Table.Cell>
                      <Table.Cell>
                        <span title={issue.reason}>
                          {issue.reason?.length > 50 
                            ? issue.reason.substring(0, 50) + '...' 
                            : issue.reason}
                        </span>
                      </Table.Cell>
                      <Table.Cell>
                        <Label 
                          size="mini" 
                          color={
                            issue.status === 'Resolved' ? 'green' :
                            issue.status === 'Fixing' ? 'blue' :
                            issue.status === 'Failed' ? 'red' :
                            'grey'
                          }
                        >
                          {issue.status}
                        </Label>
                      </Table.Cell>
                      <Table.Cell textAlign="center">
                        {issue.canAutoFix && issue.status === 'Detected' && (
                          <Button
                            size="tiny"
                            primary
                            onClick={() => handleFixSingle(issue.issueId)}
                            disabled={fixing}
                          >
                            <Icon name="wrench" />
                            Fix
                          </Button>
                        )}
                        {issue.status === 'Fixing' && (
                          <Loader active inline size="tiny" />
                        )}
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Grid.Column>
        </Grid.Row>
      </Grid>
    </Tab.Pane>
  );

  const panes = [
    {
      menuItem: {
        key: 'overview',
        icon: 'dashboard',
        content: 'Overview',
      },
      render: () => <OverviewPane />,
    },
    {
      menuItem: {
        key: 'issues',
        icon: 'warning',
        content: 'All Issues',
      },
      render: () => <IssuesPane />,
    },
  ];

  return (
    <div className="library-health">
      <Tab panes={panes} />
    </div>
  );
};

export default LibraryHealth;
