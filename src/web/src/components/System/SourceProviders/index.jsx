import * as sourceProvidersApi from '../../../lib/sourceProviders';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Header,
  Icon,
  Label,
  Loader,
  Message,
  Popup,
  Segment,
  Statistic,
} from 'semantic-ui-react';

const normalizeProvider = (provider) => ({
  active: provider.active ?? provider.Active ?? false,
  capabilities: provider.capabilities ?? provider.Capabilities ?? [],
  description: provider.description ?? provider.Description ?? '',
  disabledReason: provider.disabledReason ?? provider.DisabledReason ?? '',
  domain: provider.domain ?? provider.Domain ?? 'Any',
  id: provider.id ?? provider.Id ?? '',
  name: provider.name ?? provider.Name ?? '',
  networkPolicy: provider.networkPolicy ?? provider.NetworkPolicy ?? '',
  registered: provider.registered ?? provider.Registered ?? false,
  requiresConfiguration:
    provider.requiresConfiguration ?? provider.RequiresConfiguration ?? false,
  riskLevel: provider.riskLevel ?? provider.RiskLevel ?? 'local',
  sortOrder: provider.sortOrder ?? provider.SortOrder ?? 100,
});

const normalizeProfilePolicy = (policy) => ({
  autoDownloadEnabled:
    policy.autoDownloadEnabled ?? policy.AutoDownloadEnabled ?? false,
  notes: policy.notes ?? policy.Notes ?? '',
  profileId: policy.profileId ?? policy.ProfileId ?? '',
  profileName: policy.profileName ?? policy.ProfileName ?? '',
  providerPriority: policy.providerPriority ?? policy.ProviderPriority ?? [],
});

const riskColor = (riskLevel) => {
  switch (riskLevel) {
    case 'local':
      return 'green';
    case 'trusted-mesh':
      return 'teal';
    case 'public-network':
      return 'orange';
    case 'configured-network':
    case 'configured-lan':
      return 'blue';
    case 'high-risk':
      return 'red';
    default:
      return 'grey';
  }
};

const SourceProviders = () => {
  const [catalog, setCatalog] = useState({
    acquisitionPlanningEnabled: false,
    providers: [],
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const providers = useMemo(
    () =>
      catalog.providers
        .map(normalizeProvider)
        .sort((left, right) => left.sortOrder - right.sortOrder),
    [catalog.providers],
  );
  const profilePolicies = useMemo(
    () => (catalog.profilePolicies ?? []).map(normalizeProfilePolicy),
    [catalog.profilePolicies],
  );
  const activeCount = providers.filter((provider) => provider.active).length;
  const configuredCount = providers.filter(
    (provider) => provider.registered && !provider.requiresConfiguration,
  ).length;

  const load = async () => {
    setLoading(true);
    setError('');

    try {
      setCatalog(await sourceProvidersApi.getSourceProviders());
    } catch (loadError) {
      setError(
        loadError?.response?.data ||
          loadError?.response?.statusText ||
          loadError?.message ||
          'Unable to load source providers',
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  if (loading) {
    return (
      <Segment>
        <Loader active inline="centered" />
      </Segment>
    );
  }

  return (
    <div className="source-providers">
      {error && (
        <Message
          error
          header="Source providers unavailable"
          content={error}
        />
      )}
      <Segment>
        <div className="source-providers-header">
          <Header as="h3">
            <Icon name="random" />
            <Header.Content>
              Source Providers
              <Header.Subheader>
                Search, discovery, download, and verification sources available to acquisition planning.
              </Header.Subheader>
            </Header.Content>
          </Header>
          <Popup
            content="Reload the provider catalogue and current enablement state."
            position="top center"
            trigger={
              <Button
                aria-label="Refresh source providers"
                icon="refresh"
                onClick={load}
              />
            }
          />
        </div>
        <Statistic.Group
          className="source-providers-summary"
          size="small"
        >
          <Statistic>
            <Statistic.Value>{activeCount}</Statistic.Value>
            <Statistic.Label>Active</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>{providers.length}</Statistic.Value>
            <Statistic.Label>Known</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>{configuredCount}</Statistic.Value>
            <Statistic.Label>Ready</Statistic.Label>
          </Statistic>
        </Statistic.Group>
        {!catalog.acquisitionPlanningEnabled && (
          <Message
            info
            header="Acquisition planning is disabled"
            content="Providers remain visible here so enablement gaps are inspectable before automation is turned on."
          />
        )}
      </Segment>
      <Card.Group
        className="source-provider-grid"
        itemsPerRow={3}
        stackable
      >
        {providers.map((provider) => (
          <Card
            className="source-provider-card"
            key={provider.id}
          >
            <Card.Content>
              <div className="source-provider-title-row">
                <Card.Header>{provider.name}</Card.Header>
                <Label color={provider.active ? 'green' : 'grey'}>
                  <Icon name={provider.active ? 'check circle' : 'pause circle'} />
                  {provider.active ? 'Active' : 'Disabled'}
                </Label>
              </div>
              <Card.Meta>{provider.domain}</Card.Meta>
              <Card.Description>{provider.description}</Card.Description>
              <div className="source-provider-labels">
                <Label color={riskColor(provider.riskLevel)}>
                  {provider.riskLevel}
                </Label>
                <Label color={provider.registered ? 'blue' : 'grey'}>
                  {provider.registered ? 'Registered' : 'Not Registered'}
                </Label>
                {provider.requiresConfiguration && (
                  <Label color="yellow">Needs Config</Label>
                )}
              </div>
            </Card.Content>
            <Card.Content>
              <Label.Group size="small">
                {provider.capabilities.map((capability) => (
                  <Label key={capability}>{capability}</Label>
                ))}
              </Label.Group>
            </Card.Content>
            <Card.Content extra>
              <p className="source-provider-policy">{provider.networkPolicy}</p>
              {provider.disabledReason && (
                <p className="source-provider-disabled">{provider.disabledReason}</p>
              )}
            </Card.Content>
          </Card>
        ))}
      </Card.Group>
      <Segment>
        <Header as="h4">
          <Icon name="sliders horizontal" />
          <Header.Content>
            Profile Provider Priority
            <Header.Subheader>
              Read-only routing policy for each acquisition profile.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <Card.Group
          className="source-provider-policy-grid"
          itemsPerRow={2}
          stackable
        >
          {profilePolicies.map((policy) => (
            <Card key={policy.profileId}>
              <Card.Content>
                <div className="source-provider-title-row">
                  <Card.Header>{policy.profileName}</Card.Header>
                  <Label color={policy.autoDownloadEnabled ? 'green' : 'grey'}>
                    <Icon name={policy.autoDownloadEnabled ? 'play' : 'pause'} />
                    {policy.autoDownloadEnabled ? 'Auto' : 'Manual'}
                  </Label>
                </div>
                <Card.Description>{policy.notes}</Card.Description>
              </Card.Content>
              <Card.Content>
                <Label.Group size="small">
                  {policy.providerPriority.map((providerId, index) => (
                    <Label key={`${policy.profileId}-${providerId}`}>
                      {index + 1}. {providerId}
                    </Label>
                  ))}
                </Label.Group>
              </Card.Content>
            </Card>
          ))}
        </Card.Group>
      </Segment>
    </div>
  );
};

export default SourceProviders;
