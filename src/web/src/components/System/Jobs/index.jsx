// <copyright file="index.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Dropdown,
  Grid,
  Header,
  Icon,
  Label,
  Loader,
  Modal,
  Pagination,
  Progress,
  Segment,
  Statistic,
  Table,
} from 'semantic-ui-react';
import { formatBytes } from '../../../lib/util';
import * as jobsLib from '../../../lib/jobs';
import { toast } from 'react-toastify';
import SwarmVisualization from '../SwarmVisualization';

const Jobs = () => {
  const [jobs, setJobs] = useState([]);
  const [swarmJobs, setSwarmJobs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [swarmLoading, setSwarmLoading] = useState(true);
  const [selectedSwarmJobId, setSelectedSwarmJobId] = useState(null);
  const [showVisualization, setShowVisualization] = useState(false);
  const [filters, setFilters] = useState({
    type: null,
    status: null,
    sortBy: 'created_at',
    sortOrder: 'desc',
  });
  const [pagination, setPagination] = useState({
    limit: 20,
    offset: 0,
    total: 0,
    hasMore: false,
  });

  const fetchJobs = useCallback(async () => {
    try {
      setLoading(true);
      const response = await jobsLib.getJobs({
        type: filters.type || undefined,
        status: filters.status || undefined,
        sortBy: filters.sortBy,
        sortOrder: filters.sortOrder,
        limit: pagination.limit,
        offset: pagination.offset,
      });
      setJobs(response.jobs || []);
      setPagination((prev) => ({
        ...prev,
        total: response.total || 0,
        hasMore: response.has_more || false,
      }));
    } catch (error) {
      toast.error(
        error?.response?.data?.message ||
          error?.message ||
          'Failed to fetch jobs',
      );
      console.error('Failed to fetch jobs:', error);
    } finally {
      setLoading(false);
    }
  }, [filters, pagination.limit, pagination.offset]);

  const fetchSwarmJobs = useCallback(async () => {
    try {
      setSwarmLoading(true);
      const jobs = await jobsLib.getActiveSwarmJobs();
      setSwarmJobs(jobs);
    } catch (error) {
      console.debug('Failed to fetch swarm jobs:', error);
      setSwarmJobs([]);
    } finally {
      setSwarmLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  useEffect(() => {
    fetchSwarmJobs();
    const interval = setInterval(fetchSwarmJobs, 5000); // Refresh every 5 seconds
    return () => clearInterval(interval);
  }, [fetchSwarmJobs]);

  const analytics = useMemo(() => {
    const allJobs = [...jobs, ...swarmJobs];
    const byStatus = allJobs.reduce((acc, job) => {
      const status = job.status || 'unknown';
      acc[status] = (acc[status] || 0) + 1;
      return acc;
    }, {});

    const byType = allJobs.reduce((acc, job) => {
      const type = job.type || 'unknown';
      acc[type] = (acc[type] || 0) + 1;
      return acc;
    }, {});

    const activeCount = allJobs.filter(
      (j) => j.status === 'running' || j.status === 'pending',
    ).length;

    const completedCount = allJobs.filter((j) => j.status === 'completed').length;

    return {
      total: allJobs.length,
      active: activeCount,
      completed: completedCount,
      byStatus,
      byType,
    };
  }, [jobs, swarmJobs]);

  const handleFilterChange = (key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setPagination((prev) => ({ ...prev, offset: 0 })); // Reset to first page
  };

  const handlePageChange = (_event, { activePage }) => {
    setPagination((prev) => ({
      ...prev,
      offset: (activePage - 1) * prev.limit,
    }));
  };

  const getStatusColor = (status) => {
    switch (status?.toLowerCase()) {
      case 'running':
        return 'blue';
      case 'pending':
        return 'yellow';
      case 'completed':
        return 'green';
      case 'failed':
        return 'red';
      default:
        return 'grey';
    }
  };

  const getStatusIcon = (status) => {
    switch (status?.toLowerCase()) {
      case 'running':
        return 'spinner';
      case 'pending':
        return 'clock';
      case 'completed':
        return 'check circle';
      case 'failed':
        return 'times circle';
      default:
        return 'question circle';
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    try {
      return new Date(dateString).toLocaleString();
    } catch {
      return dateString;
    }
  };

  const totalPages = Math.ceil(pagination.total / pagination.limit);

  return (
    <div>
      {/* Analytics Overview */}
      <Segment>
        <Header as="h3">
          <Icon name="chart bar" />
          <Header.Content>Job Analytics</Header.Content>
        </Header>
        <Grid columns={4}>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>{analytics.total}</Statistic.Value>
              <Statistic.Label>Total Jobs</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                <Icon color="blue" name="spinner" />
                {analytics.active}
              </Statistic.Value>
              <Statistic.Label>Active</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                <Icon color="green" name="check circle" />
                {analytics.completed}
              </Statistic.Value>
              <Statistic.Label>Completed</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic>
              <Statistic.Value>
                {Object.keys(analytics.byType).length}
              </Statistic.Value>
              <Statistic.Label>Job Types</Statistic.Label>
            </Statistic>
          </Grid.Column>
        </Grid>
      </Segment>

      {/* Active Swarm Jobs */}
      {swarmJobs.length > 0 && (
        <Segment>
          <Header as="h3">
            <Icon name="bolt" />
            <Header.Content>
              Active Swarm Downloads
              <Header.Subheader>
                Multi-source downloads in progress
              </Header.Subheader>
            </Header.Content>
          </Header>
          {swarmLoading && <Loader active inline="centered" />}
          <Grid columns={2}>
            {swarmJobs.map((job) => (
              <Grid.Column key={job.jobId}>
                <Card fluid>
                  <Card.Content>
                    <Card.Header>
                      <Icon color="yellow" name="bolt" />
                      {job.filename?.split('/').pop() ?? 'Unknown file'}
                    </Card.Header>
                    <Card.Meta>
                      {job.activeSources ?? 0} sources •{' '}
                      {formatBytes(job.downloadedBytes ?? 0)} /{' '}
                      {formatBytes(job.totalBytes ?? 0)}
                    </Card.Meta>
                    <Progress
                      active
                      color="blue"
                      percent={job.progressPercent ?? 0}
                      progress
                      size="small"
                    />
                    {job.chunksPerSecond && (
                      <Label size="tiny">
                        <Icon name="tachometer alternate" />
                        {job.chunksPerSecond.toFixed(1)} chunks/s
                      </Label>
                    )}
                    {job.estimatedSecondsRemaining && (
                      <Label size="tiny">
                        <Icon name="clock" />
                        {Math.round(job.estimatedSecondsRemaining)}s remaining
                      </Label>
                    )}
                    <div style={{ marginTop: '0.5em' }}>
                      <Button
                        compact
                        icon
                        onClick={() => {
                          setSelectedSwarmJobId(job.jobId);
                          setShowVisualization(true);
                        }}
                        size="small"
                      >
                        <Icon name="chart bar" />
                        View Details
                      </Button>
                    </div>
                  </Card.Content>
                </Card>
              </Grid.Column>
            ))}
          </Grid>
        </Segment>
      )}

      {/* Swarm Visualization Modal */}
      <Modal
        closeIcon
        onClose={() => {
          setShowVisualization(false);
          setSelectedSwarmJobId(null);
        }}
        open={showVisualization}
        size="large"
      >
        <Modal.Header>
          <Icon name="chart bar" />
          Swarm Download Visualization
        </Modal.Header>
        <Modal.Content scrolling>
          {selectedSwarmJobId && (
            <SwarmVisualization jobId={selectedSwarmJobId} />
          )}
        </Modal.Content>
      </Modal>

      {/* Job List */}
      <Segment>
        <Header as="h3">
          <Icon name="tasks" />
          <Header.Content>All Jobs</Header.Content>
        </Header>

        {/* Filters */}
        <div style={{ marginBottom: '1em', display: 'flex', gap: '1em' }}>
          <Dropdown
            clearable
            placeholder="Filter by Type"
            selection
            options={[
              { key: 'discography', text: 'Discography', value: 'discography' },
              {
                key: 'label_crate',
                text: 'Label Crate',
                value: 'label_crate',
              },
            ]}
            value={filters.type}
            onChange={(_e, { value }) => handleFilterChange('type', value)}
          />
          <Dropdown
            clearable
            placeholder="Filter by Status"
            selection
            options={[
              { key: 'pending', text: 'Pending', value: 'pending' },
              { key: 'running', text: 'Running', value: 'running' },
              { key: 'completed', text: 'Completed', value: 'completed' },
              { key: 'failed', text: 'Failed', value: 'failed' },
            ]}
            value={filters.status}
            onChange={(_e, { value }) => handleFilterChange('status', value)}
          />
          <Dropdown
            placeholder="Sort By"
            selection
            options={[
              { key: 'created_at', text: 'Created Date', value: 'created_at' },
              { key: 'status', text: 'Status', value: 'status' },
              { key: 'id', text: 'ID', value: 'id' },
            ]}
            value={filters.sortBy}
            onChange={(_e, { value }) => handleFilterChange('sortBy', value)}
          />
          <Button
            icon
            onClick={() =>
              handleFilterChange(
                'sortOrder',
                filters.sortOrder === 'asc' ? 'desc' : 'asc',
              )
            }
            toggle
          >
            <Icon
              name={filters.sortOrder === 'asc' ? 'sort up' : 'sort down'}
            />
            {filters.sortOrder === 'asc' ? 'Ascending' : 'Descending'}
          </Button>
          <Button icon onClick={fetchJobs}>
            <Icon name="refresh" />
            Refresh
          </Button>
        </div>

        {loading ? (
          <Loader active inline="centered" />
        ) : jobs.length === 0 ? (
          <Segment placeholder>
            <Header icon>
              <Icon name="inbox" />
              No jobs found
            </Header>
            <p>No jobs match the current filters.</p>
          </Segment>
        ) : (
          <>
            <Table celled>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>ID</Table.HeaderCell>
                  <Table.HeaderCell>Type</Table.HeaderCell>
                  <Table.HeaderCell>Status</Table.HeaderCell>
                  <Table.HeaderCell>Progress</Table.HeaderCell>
                  <Table.HeaderCell>Created</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {jobs.map((job) => (
                  <Table.Row key={job.id}>
                    <Table.Cell>
                      <code>{job.id}</code>
                    </Table.Cell>
                    <Table.Cell>
                      <Label>{job.type || 'unknown'}</Label>
                    </Table.Cell>
                    <Table.Cell>
                      <Label color={getStatusColor(job.status)}>
                        <Icon name={getStatusIcon(job.status)} />
                        {job.status || 'unknown'}
                      </Label>
                    </Table.Cell>
                    <Table.Cell>
                      {job.progress ? (
                        <div>
                          <Progress
                            color={getStatusColor(job.status)}
                            percent={
                              job.progress.releases_total > 0
                                ? (job.progress.releases_done /
                                    job.progress.releases_total) *
                                  100
                                : 0
                            }
                            progress
                            size="small"
                          />
                          <div style={{ fontSize: '0.9em', marginTop: '0.25em' }}>
                            {job.progress.releases_done || 0} /{' '}
                            {job.progress.releases_total || 0} releases
                            {job.progress.releases_failed > 0 && (
                              <Label color="red" size="tiny">
                                {job.progress.releases_failed} failed
                              </Label>
                            )}
                          </div>
                        </div>
                      ) : (
                        'N/A'
                      )}
                    </Table.Cell>
                    <Table.Cell>{formatDate(job.created_at)}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>

            {totalPages > 1 && (
              <Pagination
                activePage={Math.floor(pagination.offset / pagination.limit) + 1}
                onPageChange={handlePageChange}
                totalPages={totalPages}
              />
            )}
          </>
        )}
      </Segment>
    </div>
  );
};

export default Jobs;
