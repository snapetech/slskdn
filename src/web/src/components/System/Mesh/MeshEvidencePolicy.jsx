import {
  evaluateMeshEvidenceEntries,
  formatMeshEvidenceReviewReport,
  getMeshEvidencePolicy,
  getMeshEvidencePolicySummary,
  inboundTrustTiers,
  outboundEvidenceTypes,
  parseMeshEvidenceReviewInput,
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
  Message,
  Popup,
  Segment,
  Statistic,
  TextArea,
} from 'semantic-ui-react';

const inboundOptions = inboundTrustTiers.map((tier) => ({
  key: tier.id,
  text: tier.label,
  value: tier.id,
}));

const sampleEvidence = JSON.stringify(
  {
    evidence: [
      {
        confidence: 0.92,
        provenance: {
          peerId: 'fixture-peer',
          realmId: 'fixture-realm',
          signature: 'fixture-signature',
          trustTier: 'realm',
        },
        subject: 'mbid:recording:fixture-track',
        type: 'metadataCorrection',
        witnessCount: 3,
      },
      {
        confidence: 0.82,
        containsPath: true,
        provenance: {
          peerId: 'untrusted-peer',
          signature: 'fixture-signature',
          trustTier: 'untrusted',
        },
        subject: 'private-path-example',
        type: 'releaseCompleteness',
        witnessCount: 1,
      },
    ],
  },
  null,
  2,
);

const MeshEvidencePolicy = () => {
  const [policy, setPolicy] = useState(getMeshEvidencePolicy);
  const [reviewError, setReviewError] = useState('');
  const [reviewInput, setReviewInput] = useState('');
  const [review, setReview] = useState(null);
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

  const reviewEvidence = () => {
    try {
      const entries = parseMeshEvidenceReviewInput(reviewInput);
      setReview(evaluateMeshEvidenceEntries(entries, policy));
      setReviewError('');
    } catch (error) {
      setReview(null);
      setReviewError(error instanceof Error ? error.message : String(error));
    }
  };

  const copyReviewReport = async () => {
    if (!review) return;

    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(formatMeshEvidenceReviewReport(review));
      toast.success('Mesh evidence review copied');
      return;
    }

    toast.info('Clipboard unavailable; select the review results manually');
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

      <Segment className="mesh-evidence-review">
        <Header as="h4">
          <Icon name="search" />
          Evidence Review Sandbox
          <Header.Subheader>
            Paste signed mesh evidence JSON to preview local trust, provenance,
            k-anonymity, confidence, and privacy gates before anything is applied.
          </Header.Subheader>
        </Header>
        <TextArea
          aria-label="Mesh evidence review JSON"
          className="mesh-evidence-review-input"
          onChange={(event) => setReviewInput(event.target.value)}
          placeholder={sampleEvidence}
          value={reviewInput}
        />
        <div className="mesh-evidence-review-actions">
          <Popup
            content="Load a browser-local sample containing one acceptable realm-signed item and one rejected privacy-risk item."
            position="top center"
            trigger={
              <Button
                aria-label="Load sample mesh evidence"
                onClick={() => {
                  setReviewInput(sampleEvidence);
                  setReviewError('');
                }}
              >
                <Icon name="flask" />
                Load Sample
              </Button>
            }
          />
          <Popup
            content="Evaluate the pasted evidence locally. This does not publish, query peers, or apply ranking changes."
            position="top center"
            trigger={
              <Button
                aria-label="Review mesh evidence locally"
                disabled={!reviewInput.trim()}
                onClick={reviewEvidence}
                primary
              >
                <Icon name="check circle" />
                Review Evidence
              </Button>
            }
          />
          <Popup
            content="Copy the current mesh evidence review report."
            position="top center"
            trigger={
              <Button
                aria-label="Copy mesh evidence review report"
                disabled={!review}
                onClick={copyReviewReport}
              >
                <Icon name="copy" />
                Copy Report
              </Button>
            }
          />
        </div>
        {reviewError && (
          <Message
            className="mesh-evidence-review-message"
            negative
          >
            <Message.Header>Invalid evidence JSON</Message.Header>
            <p>{reviewError}</p>
          </Message>
        )}
        {review && (
          <div className="mesh-evidence-review-results">
            <div className="mesh-evidence-review-summary">
              <Label color="blue">
                Total
                <Label.Detail>{review.summary.total}</Label.Detail>
              </Label>
              <Label color="green">
                Accepted
                <Label.Detail>{review.summary.accepted}</Label.Detail>
              </Label>
              <Label color="red">
                Rejected
                <Label.Detail>{review.summary.rejected}</Label.Detail>
              </Label>
            </div>
            <List
              divided
              relaxed
            >
              {review.results.map((entry) => (
                <List.Item key={entry.id}>
                  <List.Icon
                    color={entry.accepted ? 'green' : 'red'}
                    name={entry.accepted ? 'check circle' : 'ban'}
                    size="large"
                    verticalAlign="middle"
                  />
                  <List.Content>
                    <List.Header>
                      {entry.type} · {entry.subject}
                    </List.Header>
                    <List.Description>
                      Confidence {entry.confidence}; witnesses {entry.witnessCount};
                      provenance {entry.provenance.trustTier}
                      {entry.reasons.length > 0
                        ? `; rejected because ${entry.reasons.join(', ')}`
                        : '; accepted for local use'}
                    </List.Description>
                  </List.Content>
                </List.Item>
              ))}
            </List>
          </div>
        )}
      </Segment>
    </Segment>
  );
};

export default MeshEvidencePolicy;
