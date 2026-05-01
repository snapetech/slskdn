// <copyright file="FederatedTasteRecommendationsPanel.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import {
  fetchTasteRecommendations,
  previewTasteRecommendationGraph,
  promoteTasteRecommendationToWishlist,
  subscribeTasteRecommendationReleaseRadar,
} from '../../lib/tasteRecommendations';
import React, { useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Form,
  Header,
  Icon,
  Label,
  List,
  Message,
  Popup,
  Segment,
} from 'semantic-ui-react';

const getCreator = (workRef = {}) => workRef.creator || workRef.Creator || '';

const getTitle = (workRef = {}) => workRef.title || workRef.Title || 'Untitled recommendation';

const getSearchText = (workRef = {}) =>
  [getCreator(workRef), getTitle(workRef)].filter(Boolean).join(' ') || getTitle(workRef);

const FederatedTasteRecommendationsPanel = ({ disabled }) => {
  const [error, setError] = useState('');
  const [graphPreview, setGraphPreview] = useState(null);
  const [includeSourceActors, setIncludeSourceActors] = useState(false);
  const [limit, setLimit] = useState(20);
  const [loading, setLoading] = useState(false);
  const [minimumTrustedSources, setMinimumTrustedSources] = useState(2);
  const [recommendations, setRecommendations] = useState([]);
  const [status, setStatus] = useState('');
  const [summary, setSummary] = useState(null);

  const loadRecommendations = async () => {
    setError('');
    setLoading(true);
    try {
      const response = await fetchTasteRecommendations({
        includeSourceActors,
        limit: Number(limit) || 20,
        minimumTrustedSources: Number(minimumTrustedSources) || 2,
      });
      setSummary(response.data);
      setRecommendations(response.data?.recommendations || []);
      setStatus(
        `Loaded ${response.data?.recommendations?.length || 0} privacy-filtered recommendation${
          response.data?.recommendations?.length === 1 ? '' : 's'
        }.`,
      );
    } catch (loadError) {
      setError(
        loadError?.response?.data ||
          loadError?.message ||
          'Unable to load federated taste recommendations.',
      );
    } finally {
      setLoading(false);
    }
  };

  const promoteToWishlist = async (recommendation) => {
    try {
      const response = await promoteTasteRecommendationToWishlist({
        note: 'Promoted from federated taste recommendation review.',
        workRef: recommendation.workRef,
      });
      setStatus(response.data?.message || `Promoted ${getTitle(recommendation.workRef)} to Wishlist.`);
    } catch (promoteError) {
      toast.error(promoteError?.response?.data?.message || promoteError.message);
    }
  };

  const subscribeRadar = async (recommendation) => {
    try {
      const response = await subscribeTasteRecommendationReleaseRadar({
        scope: 'trusted',
        workRef: recommendation.workRef,
      });
      setStatus(response.data?.message || `Subscribed ${getCreator(recommendation.workRef)} to Release Radar.`);
    } catch (subscribeError) {
      toast.error(subscribeError?.response?.data?.message || subscribeError.message);
    }
  };

  const previewGraph = async (recommendation) => {
    try {
      const response = await previewTasteRecommendationGraph({
        workRef: recommendation.workRef,
      });
      setGraphPreview(response.data);
      setStatus(`Previewed Discovery Graph for ${getTitle(recommendation.workRef)}.`);
    } catch (previewError) {
      toast.error(previewError?.response?.data?.message || previewError.message);
    }
  };

  if (disabled) {
    return (
      <Segment raised>
        <Header as="h4">Federated Taste Recommendations</Header>
        <p>Connect to the server to load privacy-filtered recommendations.</p>
      </Segment>
    );
  }

  return (
    <Segment
      loading={loading}
      raised
    >
      <Header as="h4">Federated Taste Recommendations</Header>
      <Form>
        <Form.Group widths="equal">
          <Form.Input
            aria-label="Taste recommendation limit"
            label="Limit"
            min={1}
            onChange={(event) => setLimit(event.target.value)}
            type="number"
            value={limit}
          />
          <Form.Input
            aria-label="Minimum trusted taste sources"
            label="Minimum trusted sources"
            min={1}
            onChange={(event) => setMinimumTrustedSources(event.target.value)}
            type="number"
            value={minimumTrustedSources}
          />
          <Form.Field>
            <label>Reveal source actors</label>
            <Checkbox
              aria-label="Reveal federated recommendation source actors"
              checked={includeSourceActors}
              onChange={(_event, data) => setIncludeSourceActors(data.checked)}
              toggle
            />
          </Form.Field>
        </Form.Group>
        <Popup
          content="Load privacy-filtered recommendations from followed federated actors. Source actors remain hidden unless explicitly revealed."
          position="top center"
          trigger={
            <Button
              onClick={loadRecommendations}
              primary
              type="button"
            >
              <Icon name="users" />
              Load Recommendations
            </Button>
          }
        />
      </Form>
      {status && <Message compact size="mini">{status}</Message>}
      {error && <Message compact error size="mini">{String(error)}</Message>}
      {summary && (
        <div className="search-acquisition-profile-strip">
          <Label basic>
            Trusted actors
            <Label.Detail>{summary.trustedActorCount}</Label.Detail>
          </Label>
          <Label basic>
            Candidates
            <Label.Detail>{summary.candidateCount}</Label.Detail>
          </Label>
          <Label basic>
            Minimum sources
            <Label.Detail>{summary.minimumTrustedSources}</Label.Detail>
          </Label>
        </div>
      )}
      {graphPreview && (
        <Message compact size="mini">
          Graph preview: {graphPreview.nodeCount} nodes, {graphPreview.edgeCount} edges.
        </Message>
      )}
      <List divided relaxed>
        {recommendations.map((recommendation) => {
          const title = getTitle(recommendation.workRef);
          const creator = getCreator(recommendation.workRef);

          return (
            <List.Item key={recommendation.workRef?.id || `${creator}-${title}`}>
              <List.Icon name="user friends" />
              <List.Content>
                <List.Header>{[creator, title].filter(Boolean).join(' - ') || title}</List.Header>
                <List.Description>
                  {recommendation.trustedSourceCount} trusted sources · score {Math.round((recommendation.score || 0) * 100)}%
                </List.Description>
                {(recommendation.reasons || []).map((reason) => (
                  <Label basic key={reason}>{reason}</Label>
                ))}
                {includeSourceActors && recommendation.sourceActors?.length > 0 && (
                  <div className="search-acquisition-profile-summary">
                    {recommendation.sourceActors.join(', ')}
                  </div>
                )}
                <Popup
                  content="Promote this WorkRef to Wishlist through the backend review handoff without starting a download."
                  position="top center"
                  trigger={
                    <Button
                      aria-label={`Promote ${title} taste recommendation to Wishlist`}
                      onClick={() => promoteToWishlist(recommendation)}
                      size="mini"
                      type="button"
                    >
                      <Icon name="heart" />
                      Wishlist
                    </Button>
                  }
                />
                <Popup
                  content="Subscribe this artist to Release Radar using trusted scope."
                  position="top center"
                  trigger={
                    <Button
                      aria-label={`Subscribe ${title} taste recommendation to Release Radar`}
                      onClick={() => subscribeRadar(recommendation)}
                      size="mini"
                      type="button"
                    >
                      <Icon name="rss" />
                      Radar
                    </Button>
                  }
                />
                <Popup
                  content="Preview nearby Discovery Graph evidence for this WorkRef."
                  position="top center"
                  trigger={
                    <Button
                      aria-label={`Preview ${title} taste recommendation graph`}
                      onClick={() => previewGraph(recommendation)}
                      size="mini"
                      type="button"
                    >
                      <Icon name="share alternate" />
                      Graph
                    </Button>
                  }
                />
              </List.Content>
            </List.Item>
          );
        })}
      </List>
    </Segment>
  );
};

export default FederatedTasteRecommendationsPanel;
