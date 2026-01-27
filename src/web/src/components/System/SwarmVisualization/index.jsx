// <copyright file="index.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Card,
  Grid,
  Header,
  Icon,
  Label,
  Loader,
  Progress,
  Segment,
  Statistic,
  Table,
} from 'semantic-ui-react';
import { formatBytes } from '../../../lib/util';
import * as jobsLib from '../../../lib/jobs';

const SwarmVisualization = ({ jobId }) => {
  const [jobStatus, setJobStatus] = useState(null);
  const [traceSummary, setTraceSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchData = useCallback(async () => {
    if (!jobId) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);

      const [status, summary] = await Promise.allSettled([
        jobsLib.getSwarmJobStatus(jobId),
        jobsLib.getSwarmTraceSummary(jobId),
      ]);

      if (status.status === 'fulfilled') {
        setJobStatus(status.value);
      } else {
        setError(status.reason?.message || 'Failed to fetch job status');
      }

      if (summary.status === 'fulfilled' && summary.value) {
        setTraceSummary(summary.value);
      }
      // Trace summary is optional - don't error if not available
    } catch (err) {
      setError(err?.message || 'Failed to fetch swarm data');
      console.error('Failed to fetch swarm visualization data:', err);
    } finally {
      setLoading(false);
    }
  }, [jobId]);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 2000); // Refresh every 2 seconds
    return () => clearInterval(interval);
  }, [fetchData]);

  const peerContributions = useMemo(() => {
    if (traceSummary?.peers && traceSummary.peers.length > 0) {
      return traceSummary.peers.map((peer) => ({
        peerId: peer.peerId,
        chunksCompleted: peer.chunksCompleted || 0,
        chunksFailed: peer.chunksFailed || 0,
        chunksTimedOut: peer.chunksTimedOut || 0,
        bytesServed: peer.bytesServed || 0,
        successRate:
          peer.chunksCompleted + peer.chunksFailed + peer.chunksTimedOut > 0
            ? (peer.chunksCompleted /
                (peer.chunksCompleted +
                  peer.chunksFailed +
                  peer.chunksTimedOut)) *
              100
            : 0,
      }));
    }
    return [];
  }, [traceSummary]);

  const chunkHeatmap = useMemo(() => {
    if (!jobStatus || !traceSummary) return null;

    const totalChunks = jobStatus.totalChunks || 0;
    const completedChunks = jobStatus.completedChunks || 0;
    const chunksPerRow = Math.ceil(Math.sqrt(totalChunks)) || 20;

    // Create a simple grid representation
    const rows = [];
    for (let i = 0; i < totalChunks; i += chunksPerRow) {
      const rowChunks = [];
      for (let j = 0; j < chunksPerRow && i + j < totalChunks; j++) {
        const chunkIndex = i + j;
        const isCompleted = chunkIndex < completedChunks;
        rowChunks.push({
          index: chunkIndex,
          completed: isCompleted,
        });
      }
      rows.push(rowChunks);
    }

    return { rows, chunksPerRow };
  }, [jobStatus, traceSummary]);

  if (loading && !jobStatus) {
    return (
      <Segment>
        <Loader active inline="centered" />
      </Segment>
    );
  }

  if (error && !jobStatus) {
    return (
      <Segment>
        <Header as="h4" color="red">
          <Icon name="exclamation triangle" />
          <Header.Content>Error Loading Swarm Data</Header.Content>
        </Header>
        <p>{error}</p>
      </Segment>
    );
  }

  if (!jobStatus) {
    return (
      <Segment placeholder>
        <Header icon>
          <Icon name="info circle" />
          No swarm job selected
        </Header>
        <p>Select a swarm download job to view visualization</p>
      </Segment>
    );
  }

  const percentComplete =
    jobStatus.totalChunks > 0
      ? (jobStatus.completedChunks / jobStatus.totalChunks) * 100
      : 0;

  return (
    <div>
      {/* Job Overview */}
      <Segment>
        <Header as="h3">
          <Icon name="bolt" />
          <Header.Content>Swarm Download Status</Header.Content>
        </Header>
        <Grid columns={4}>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                {jobStatus.completedChunks || 0} / {jobStatus.totalChunks || 0}
              </Statistic.Value>
              <Statistic.Label>Chunks</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                {jobStatus.activeWorkers || 0}
              </Statistic.Value>
              <Statistic.Label>Active Workers</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                {jobStatus.chunksPerSecond
                  ? jobStatus.chunksPerSecond.toFixed(1)
                  : '0.0'}
              </Statistic.Value>
              <Statistic.Label>Chunks/Second</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                {jobStatus.estimatedSecondsRemaining > 0
                  ? `${Math.round(jobStatus.estimatedSecondsRemaining)}s`
                  : 'N/A'}
              </Statistic.Value>
              <Statistic.Label>ETA</Statistic.Label>
            </Statistic>
          </Grid.Column>
        </Grid>
        <Progress
          active
          color="blue"
          percent={percentComplete}
          progress
          size="large"
          style={{ marginTop: '1em' }}
        />
        <div style={{ marginTop: '0.5em', fontSize: '0.9em' }}>
          {formatBytes(jobStatus.bytesDownloaded || 0)} /{' '}
          {formatBytes((jobStatus.totalChunks || 0) * 512 * 1024)}
        </div>
      </Segment>

      {/* Peer Contributions */}
      {peerContributions.length > 0 && (
        <Segment>
          <Header as="h3">
            <Icon name="users" />
            <Header.Content>Peer Contributions</Header.Content>
          </Header>
          <Table celled>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Peer</Table.HeaderCell>
                <Table.HeaderCell>Chunks Completed</Table.HeaderCell>
                <Table.HeaderCell>Chunks Failed</Table.HeaderCell>
                <Table.HeaderCell>Bytes Served</Table.HeaderCell>
                <Table.HeaderCell>Success Rate</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {peerContributions.map((peer) => (
                <Table.Row key={peer.peerId}>
                  <Table.Cell>
                    <Icon name="user" />
                    {peer.peerId}
                  </Table.Cell>
                  <Table.Cell>
                    <Label color="green">{peer.chunksCompleted}</Label>
                  </Table.Cell>
                  <Table.Cell>
                    {peer.chunksFailed > 0 && (
                      <Label color="red">{peer.chunksFailed}</Label>
                    )}
                    {peer.chunksFailed === 0 && '-'}
                  </Table.Cell>
                  <Table.Cell>{formatBytes(peer.bytesServed)}</Table.Cell>
                  <Table.Cell>
                    <Progress
                      color={peer.successRate >= 80 ? 'green' : peer.successRate >= 50 ? 'yellow' : 'red'}
                      percent={peer.successRate}
                      progress
                      size="small"
                    />
                    <span style={{ marginLeft: '0.5em', fontSize: '0.9em' }}>
                      {peer.successRate.toFixed(1)}%
                    </span>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </Segment>
      )}

      {/* Chunk Assignment Heatmap */}
      {chunkHeatmap && (
        <Segment>
          <Header as="h3">
            <Icon name="grid layout" />
            <Header.Content>Chunk Progress Heatmap</Header.Content>
            <Header.Subheader>
              Visual representation of chunk completion status
            </Header.Subheader>
          </Header>
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: '2px',
              maxHeight: '400px',
              overflow: 'auto',
            }}
          >
            {chunkHeatmap.rows.map((row, rowIdx) => (
              <div
                key={rowIdx}
                style={{
                  display: 'flex',
                  gap: '2px',
                  flexWrap: 'wrap',
                }}
              >
                {row.map((chunk) => (
                  <div
                    key={chunk.index}
                    style={{
                      width: '12px',
                      height: '12px',
                      backgroundColor: chunk.completed ? '#21ba45' : '#767676',
                      borderRadius: '2px',
                      cursor: 'pointer',
                      title: `Chunk ${chunk.index + 1}: ${chunk.completed ? 'Completed' : 'Pending'}`,
                    }}
                  />
                ))}
              </div>
            ))}
          </div>
          <div style={{ marginTop: '0.5em', fontSize: '0.9em', display: 'flex', gap: '1em' }}>
            <div>
              <span
                style={{
                  display: 'inline-block',
                  width: '12px',
                  height: '12px',
                  backgroundColor: '#21ba45',
                  borderRadius: '2px',
                  marginRight: '0.25em',
                }}
              />
              Completed
            </div>
            <div>
              <span
                style={{
                  display: 'inline-block',
                  width: '12px',
                  height: '12px',
                  backgroundColor: '#767676',
                  borderRadius: '2px',
                  marginRight: '0.25em',
                }}
              />
              Pending
            </div>
          </div>
        </Segment>
      )}

      {/* Performance Metrics */}
      {traceSummary && (
        <Segment>
          <Header as="h3">
            <Icon name="chart line" />
            <Header.Content>Performance Metrics</Header.Content>
          </Header>
          <Grid columns={3}>
            <Grid.Column>
              <Statistic>
                <Statistic.Value>
                  {traceSummary.totalEvents || 0}
                </Statistic.Value>
                <Statistic.Label>Total Events</Statistic.Label>
              </Statistic>
            </Grid.Column>
            <Grid.Column>
              <Statistic>
                <Statistic.Value>
                  {traceSummary.duration
                    ? (() => {
                        // TimeSpan serializes as string (e.g., "00:01:23") or object with properties
                        if (typeof traceSummary.duration === 'string') {
                          // Parse "HH:MM:SS" format
                          const parts = traceSummary.duration.split(':');
                          if (parts.length === 3) {
                            const totalSeconds =
                              parseInt(parts[0], 10) * 3600 +
                              parseInt(parts[1], 10) * 60 +
                              parseInt(parts[2], 10);
                            return `${totalSeconds}s`;
                          }
                        } else if (
                          typeof traceSummary.duration === 'object' &&
                          traceSummary.duration
                        ) {
                          const dur = traceSummary.duration;
                          if (dur.totalSeconds !== undefined) {
                            return `${Math.round(dur.totalSeconds)}s`;
                          }
                          if (dur.seconds !== undefined) {
                            return `${Math.round(dur.seconds)}s`;
                          }
                        }
                        return 'N/A';
                      })()
                    : 'N/A'}
                </Statistic.Value>
                <Statistic.Label>Duration</Statistic.Label>
              </Statistic>
            </Grid.Column>
            <Grid.Column>
              <Statistic>
                <Statistic.Value>
                  {traceSummary.rescueInvoked ? (
                    <Icon color="orange" name="exclamation triangle" />
                  ) : (
                    <Icon color="green" name="check circle" />
                  )}
                </Statistic.Value>
                <Statistic.Label>
                  {traceSummary.rescueInvoked ? 'Rescue Invoked' : 'Normal'}
                </Statistic.Label>
              </Statistic>
            </Grid.Column>
          </Grid>
          {traceSummary.bytesBySource &&
            Object.keys(traceSummary.bytesBySource).length > 0 && (
              <div style={{ marginTop: '1em' }}>
                <Header as="h4" size="small">
                  Bytes by Source
                </Header>
                <Table size="small">
                  <Table.Body>
                    {Object.entries(traceSummary.bytesBySource)
                      .sort((a, b) => b[1] - a[1])
                      .map(([source, bytes]) => (
                        <Table.Row key={source}>
                          <Table.Cell>{source}</Table.Cell>
                          <Table.Cell>{formatBytes(bytes)}</Table.Cell>
                        </Table.Row>
                      ))}
                  </Table.Body>
                </Table>
              </div>
            )}
        </Segment>
      )}

      {!traceSummary && (
        <Segment>
          <Header as="h4" size="small" color="grey">
            <Icon name="info circle" />
            <Header.Content>Trace Data Not Available</Header.Content>
          </Header>
          <p>
            Detailed peer contribution and performance metrics require trace data.
            This may not be available for all swarm downloads.
          </p>
        </Segment>
      )}
    </div>
  );
};

export default SwarmVisualization;
