import {
  getMeshEvidencePolicy,
  getMeshEvidencePolicySummary,
  inboundTrustTiers,
  outboundEvidenceTypes,
  resetMeshEvidencePolicy,
  setMeshEvidenceInboundTrustTier,
  setMeshEvidenceOutboundEnabled,
} from '../../../lib/meshEvidencePolicy';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Dropdown,
  Header,
  Icon,
  Label,
  List,
  Popup,
  Segment,
  Statistic,
} from 'semantic-ui-react';

const inboundOptions = inboundTrustTiers.map((tier) => ({
  key: tier.id,
  text: tier.label,
  value: tier.id,
}));

const MeshEvidencePolicy = () => {
  const [policy, setPolicy] = useState(getMeshEvidencePolicy);
  const summary = useMemo(() => getMeshEvidencePolicySummary(policy), [policy]);

  const setInboundTier = (_event, { value }) => {
    setPolicy(setMeshEvidenceInboundTrustTier(value));
    toast.info('Mesh evidence inbound trust policy updated');
  };

  const toggleOutbound = (evidenceType, enabled) => {
    setPolicy(setMeshEvidenceOutboundEnabled(evidenceType.id, enabled));
    toast.info(
      `${evidenceType.label} publication ${enabled ? 'enabled' : 'disabled'}`,
    );
  };

  const resetPolicy = () => {
    setPolicy(resetMeshEvidencePolicy());
    toast.info('Mesh evidence policy reset to private defaults');
  };

  return (
    <Segment className="mesh-evidence-policy">
      <div className="mesh-evidence-policy-header">
        <Header as="h3">
          <Icon name="certificate" />
          <Header.Content>
            Mesh Evidence Policy
            <Header.Subheader>
              Local controls for trusted metadata evidence. Nothing is published unless explicitly enabled here.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <Popup
          content="Reset inbound and outbound mesh evidence controls to private defaults."
          position="top center"
          trigger={
            <Button
              aria-label="Reset mesh evidence policy to private defaults"
              icon="undo"
              onClick={resetPolicy}
            />
          }
        />
      </div>

      <Statistic.Group
        size="small"
        widths="three"
      >
        <Statistic color={summary.inboundEnabled ? 'blue' : 'grey'}>
          <Statistic.Value>{summary.inboundEnabled ? 'On' : 'Off'}</Statistic.Value>
          <Statistic.Label>Inbound Evidence</Statistic.Label>
        </Statistic>
        <Statistic color={summary.outboundEnabled ? 'orange' : 'grey'}>
          <Statistic.Value>{summary.enabledOutbound.length}</Statistic.Value>
          <Statistic.Label>Outbound Types</Statistic.Label>
        </Statistic>
        <Statistic color={policy.provenanceRequired ? 'green' : 'red'}>
          <Statistic.Value>
            {policy.provenanceRequired ? 'Required' : 'Optional'}
          </Statistic.Value>
          <Statistic.Label>Provenance</Statistic.Label>
        </Statistic>
      </Statistic.Group>

      <div className="mesh-evidence-policy-grid">
        <Segment>
          <Header as="h4">Inbound Trust Gate</Header>
          <Dropdown
            aria-label="Mesh evidence inbound trust tier"
            fluid
            onChange={setInboundTier}
            options={inboundOptions}
            selection
            value={policy.inboundTrustTier}
          />
          <p className="mesh-evidence-policy-help">
            {summary.inboundTier.description}
          </p>
          <Label color="green">
            <Icon name="lock" />
            Provenance required
          </Label>
        </Segment>

        <Segment>
          <Header as="h4">Outbound Publication</Header>
          <List
            divided
            relaxed
          >
            {outboundEvidenceTypes.map((evidenceType) => {
              const enabled = policy.outbound[evidenceType.id] === true;

              return (
                <List.Item key={evidenceType.id}>
                  <List.Content floated="right">
                    <Popup
                      content={`${enabled ? 'Disable' : 'Enable'} publication of ${evidenceType.label}. This remains local policy state until backend federation is wired.`}
                      position="top center"
                      trigger={
                        <Checkbox
                          aria-label={`${enabled ? 'Disable' : 'Enable'} ${evidenceType.label} publication`}
                          checked={enabled}
                          onChange={(_event, { checked }) =>
                            toggleOutbound(evidenceType, checked)
                          }
                          toggle
                        />
                      }
                    />
                  </List.Content>
                  <List.Icon
                    color={enabled ? 'orange' : 'grey'}
                    name={enabled ? 'share alternate' : 'lock'}
                    size="large"
                    verticalAlign="middle"
                  />
                  <List.Content>
                    <List.Header>{evidenceType.label}</List.Header>
                    <List.Description>
                      {evidenceType.description}
                    </List.Description>
                  </List.Content>
                </List.Item>
              );
            })}
          </List>
        </Segment>
      </div>
    </Segment>
  );
};

export default MeshEvidencePolicy;
