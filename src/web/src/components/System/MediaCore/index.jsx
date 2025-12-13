import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Grid, Header, Icon, Input, Label, List, Loader, Message, Segment, Statistic } from 'semantic-ui-react';
import * as mediacore from '../../../lib/mediacore';

const MediaCore = () => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Form state
  const [externalId, setExternalId] = useState('');
  const [contentId, setContentId] = useState('');
  const [resolveId, setResolveId] = useState('');
  const [resolvedContent, setResolvedContent] = useState(null);
  const [registering, setRegistering] = useState(false);
  const [resolving, setResolving] = useState(false);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await mediacore.getContentIdStats();
        setStats(data);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();

    // Refresh stats every 60 seconds
    const interval = setInterval(fetchStats, 60000);
    return () => clearInterval(interval);
  }, []);

  const handleRegister = async () => {
    if (!externalId.trim() || !contentId.trim()) return;

    try {
      setRegistering(true);
      await mediacore.registerContentId(externalId.trim(), contentId.trim());
      setExternalId('');
      setContentId('');

      // Refresh stats
      const data = await mediacore.getContentIdStats();
      setStats(data);
    } catch (err) {
      setError(`Failed to register: ${err.message}`);
    } finally {
      setRegistering(false);
    }
  };

  const handleResolve = async () => {
    if (!resolveId.trim()) return;

    try {
      setResolving(true);
      setResolvedContent(null);
      const result = await mediacore.resolveContentId(resolveId.trim());
      setResolvedContent(result);
    } catch (err) {
      setResolvedContent({ error: err.message });
    } finally {
      setResolving(false);
    }
  };

  if (loading && !stats) {
    return (
      <Segment>
        <Loader active inline="centered">
          Loading MediaCore statistics...
        </Loader>
      </Segment>
    );
  }

  if (error && !stats) {
    return (
      <Message error>
        <Message.Header>Failed to load MediaCore statistics</Message.Header>
        <p>{error}</p>
      </Message>
    );
  }

  return (
    <div>
      <Header as="h2">
        <Icon name="database" />
        MediaCore ContentID Registry
      </Header>

      <Grid stackable>
        {/* Statistics Overview */}
        <Grid.Column width={16}>
          <Segment>
            <Header as="h3">Registry Statistics</Header>
            <Statistic.Group size="small">
              <Statistic>
                <Statistic.Value>{stats?.totalMappings || 0}</Statistic.Value>
                <Statistic.Label>Total Mappings</Statistic.Label>
              </Statistic>
              <Statistic>
                <Statistic.Value>{stats?.totalDomains || 0}</Statistic.Value>
                <Statistic.Label>Domains</Statistic.Label>
              </Statistic>
            </Statistic.Group>

            {stats?.mappingsByDomain && Object.keys(stats.mappingsByDomain).length > 0 && (
              <div style={{ marginTop: '1em' }}>
                <Header as="h4">Mappings by Domain</Header>
                <List horizontal>
                  {Object.entries(stats.mappingsByDomain).map(([domain, count]) => (
                    <List.Item key={domain}>
                      <Label>
                        {domain}
                        <Label.Detail>{count}</Label.Detail>
                      </Label>
                    </List.Item>
                  ))}
                </List>
              </div>
            )}
          </Segment>
        </Grid.Column>

        {/* Register New Mapping */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="plus" />
                Register ContentID Mapping
              </Card.Header>
              <Card.Description>
                Map an external identifier to an internal ContentID
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>External ID</label>
                  <Input
                    placeholder="e.g., mb:recording:12345-6789-..."
                    value={externalId}
                    onChange={(e) => setExternalId(e.target.value)}
                  />
                </Form.Field>
                <Form.Field>
                  <label>Content ID</label>
                  <Input
                    placeholder="e.g., content:mb:recording:12345-6789-..."
                    value={contentId}
                    onChange={(e) => setContentId(e.target.value)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={registering}
                  disabled={!externalId.trim() || !contentId.trim() || registering}
                  onClick={handleRegister}
                >
                  Register Mapping
                </Button>
              </Form>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Resolve External ID */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search" />
                Resolve External ID
              </Card.Header>
              <Card.Description>
                Find the ContentID for an external identifier
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>External ID to Resolve</label>
                  <Input
                    placeholder="Enter external ID to resolve..."
                    value={resolveId}
                    onChange={(e) => setResolveId(e.target.value)}
                    action={
                      <Button
                        primary
                        loading={resolving}
                        disabled={!resolveId.trim() || resolving}
                        onClick={handleResolve}
                      >
                        Resolve
                      </Button>
                    }
                  />
                </Form.Field>
              </Form>

              {resolvedContent && (
                <div style={{ marginTop: '1em' }}>
                  {resolvedContent.error ? (
                    <Message error>
                      <p>{resolvedContent.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Resolved Successfully</Message.Header>
                      <p>
                        <strong>External ID:</strong> {resolvedContent.externalId}<br />
                        <strong>Content ID:</strong> {resolvedContent.contentId}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>
      </Grid>
    </div>
  );
};

export default MediaCore;
