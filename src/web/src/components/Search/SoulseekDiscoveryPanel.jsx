// <copyright file="SoulseekDiscoveryPanel.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import * as soulseekDiscovery from '../../lib/soulseekDiscovery';
import * as wishlist from '../../lib/wishlist';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Form,
  Header,
  Icon,
  Label,
  List,
  Message,
  Popup,
  Segment,
} from 'semantic-ui-react';

const getValue = (value, camel, pascal, fallback = undefined) =>
  value?.[camel] ?? value?.[pascal] ?? fallback;

const normalizeRecommendation = (recommendation) => ({
  item: getValue(recommendation, 'item', 'Item', ''),
  score: getValue(recommendation, 'score', 'Score', null),
});

const normalizeUser = (user) => {
  if (typeof user === 'string') {
    return { rating: null, username: user };
  }

  return {
    rating: getValue(user, 'rating', 'Rating', null),
    username: getValue(user, 'username', 'Username', ''),
  };
};

const getRecommendations = (payload) =>
  (getValue(payload, 'recommendations', 'Recommendations', []) || [])
    .map(normalizeRecommendation)
    .filter((recommendation) => recommendation.item);

const getUnrecommendations = (payload) =>
  (getValue(payload, 'unrecommendations', 'Unrecommendations', []) || [])
    .map(normalizeRecommendation)
    .filter((recommendation) => recommendation.item);

const getUsernames = (payload) =>
  (getValue(payload, 'usernames', 'Usernames', []) || [])
    .map(normalizeUser)
    .filter((user) => user.username);

const errorMessage = (error, fallback) =>
  error?.response?.data?.message ||
  error?.response?.data ||
  error?.message ||
  fallback;

const SoulseekDiscoveryPanel = ({ disabled, onSearch }) => {
  const [error, setError] = useState('');
  const [interest, setInterest] = useState('');
  const [item, setItem] = useState('');
  const [loading, setLoading] = useState(false);
  const [recommendations, setRecommendations] = useState([]);
  const [similarUsers, setSimilarUsers] = useState([]);
  const [status, setStatus] = useState('');
  const [title, setTitle] = useState('');
  const [unrecommendations, setUnrecommendations] = useState([]);
  const [userInterests, setUserInterests] = useState(null);
  const [username, setUsername] = useState('');

  const hasResults = useMemo(
    () =>
      recommendations.length > 0 ||
      unrecommendations.length > 0 ||
      similarUsers.length > 0 ||
      userInterests,
    [recommendations.length, similarUsers.length, unrecommendations.length, userInterests],
  );

  const clearResults = () => {
    setRecommendations([]);
    setSimilarUsers([]);
    setUnrecommendations([]);
    setUserInterests(null);
  };

  const run = async (label, action) => {
    setError('');
    setLoading(true);
    try {
      await action();
    } catch (actionError) {
      setError(errorMessage(actionError, `Unable to ${label}.`));
    } finally {
      setLoading(false);
    }
  };

  const loadRecommendations = (global = false) =>
    run(global ? 'load global recommendations' : 'load recommendations', async () => {
      const response = global
        ? await soulseekDiscovery.getGlobalRecommendations()
        : await soulseekDiscovery.getRecommendations();
      const payload = response.data || {};

      clearResults();
      setRecommendations(getRecommendations(payload));
      setUnrecommendations(getUnrecommendations(payload));
      setTitle(global ? 'Global recommendations' : 'My recommendations');
      setStatus(
        `Loaded ${getRecommendations(payload).length} recommendation${
          getRecommendations(payload).length === 1 ? '' : 's'
        }.`,
      );
    });

  const loadItemRecommendations = () => {
    const trimmed = item.trim();
    if (!trimmed) {
      toast.error('Item is required');
      return;
    }

    run('load item recommendations', async () => {
      const response = await soulseekDiscovery.getItemRecommendations({
        item: trimmed,
      });

      clearResults();
      setRecommendations(getRecommendations(response.data || {}));
      setTitle(`Recommendations for ${trimmed}`);
      setStatus(`Loaded related recommendation seeds for ${trimmed}.`);
    });
  };

  const loadSimilarUsers = () =>
    run('load similar users', async () => {
      const response = await soulseekDiscovery.getSimilarUsers();

      clearResults();
      setSimilarUsers((response.data || []).map(normalizeUser).filter((user) => user.username));
      setTitle('Similar users');
      setStatus(`Loaded ${(response.data || []).length} similar user${(response.data || []).length === 1 ? '' : 's'}.`);
    });

  const loadItemSimilarUsers = () => {
    const trimmed = item.trim();
    if (!trimmed) {
      toast.error('Item is required');
      return;
    }

    run('load item similar users', async () => {
      const response = await soulseekDiscovery.getItemSimilarUsers({
        item: trimmed,
      });

      clearResults();
      setSimilarUsers(getUsernames(response.data || {}));
      setTitle(`Users similar to ${trimmed}`);
      setStatus(`Loaded users associated with ${trimmed}.`);
    });
  };

  const loadUserInterests = (target = username) => {
    const trimmed = `${target || ''}`.trim();
    if (!trimmed) {
      toast.error('Username is required');
      return;
    }

    run('load user interests', async () => {
      const response = await soulseekDiscovery.getUserInterests({
        username: trimmed,
      });

      clearResults();
      setUserInterests(response.data || {});
      setTitle(`${trimmed} interests`);
      setStatus(`Loaded native interests for ${trimmed}.`);
    });
  };

  const updateInterest = (hated = false, remove = false) => {
    const trimmed = interest.trim();
    if (!trimmed) {
      toast.error('Interest is required');
      return;
    }

    const action = hated
      ? remove
        ? soulseekDiscovery.removeHatedInterest
        : soulseekDiscovery.addHatedInterest
      : remove
        ? soulseekDiscovery.removeInterest
        : soulseekDiscovery.addInterest;

    run('update interests', async () => {
      await action({ item: trimmed });
      setStatus(`${remove ? 'Removed' : 'Added'} ${trimmed} ${hated ? 'as a hated interest' : 'as an interest'}.`);
      setInterest('');
    });
  };

  const addToWishlist = async (searchText) => {
    try {
      await wishlist.create({
        autoDownload: false,
        enabled: false,
        filter: 'source:soulseek-native-discovery; review-only',
        maxResults: 25,
        searchText,
      });
      toast.success(`Added ${searchText} to Wishlist for review`);
    } catch (wishlistError) {
      toast.error(errorMessage(wishlistError, 'Unable to add to Wishlist.'));
    }
  };

  const renderRecommendation = (recommendation) => (
    <List.Item key={`${recommendation.item}-${recommendation.score ?? 'n'}`}>
      <List.Content floated="right">
        <Popup
          content="Start a normal Soulseek search for this recommendation."
          trigger={
            <Button
              aria-label={`Search ${recommendation.item}`}
              icon="search"
              onClick={() => onSearch?.(recommendation.item)}
              size="mini"
            />
          }
        />
        <Popup
          content="Save this raw recommendation to Wishlist for manual review."
          trigger={
            <Button
              aria-label={`Add ${recommendation.item} to Wishlist`}
              icon="bookmark outline"
              onClick={() => addToWishlist(recommendation.item)}
              size="mini"
            />
          }
        />
      </List.Content>
      <List.Icon name="music" />
      <List.Content>
        <List.Header>{recommendation.item}</List.Header>
        {recommendation.score !== null && (
          <List.Description>
            <Label size="mini">score {recommendation.score}</Label>
          </List.Description>
        )}
      </List.Content>
    </List.Item>
  );

  const renderSimilarUser = (user) => (
    <List.Item key={user.username}>
      <List.Content floated="right">
        <Popup
          content="Load this user's native Soulseek interests."
          trigger={
            <Button
              aria-label={`Load ${user.username} interests`}
              icon="heart outline"
              onClick={() => loadUserInterests(user.username)}
              size="mini"
            />
          }
        />
      </List.Content>
      <List.Icon name="user" />
      <List.Content>
        <List.Header>{user.username}</List.Header>
        {user.rating !== null && (
          <List.Description>
            <Label size="mini">rating {user.rating}</Label>
          </List.Description>
        )}
      </List.Content>
    </List.Item>
  );

  const renderInterestList = (label, values, color) => (
    <div style={{ marginBottom: '0.75em' }}>
      <Header
        as="h5"
        style={{ marginBottom: '0.35em' }}
      >
        {label}
      </Header>
      {values.length === 0 ? (
        <span style={{ opacity: 0.65 }}>None reported</span>
      ) : (
        values.map((value) => (
          <Label
            color={color}
            key={value}
            size="small"
          >
            {value}
          </Label>
        ))
      )}
    </div>
  );

  if (disabled) {
    return (
      <Segment raised>
        <Header as="h4">Soulseek Native Discovery</Header>
        <p>Connect to the server to use native Soulseek interests and recommendations.</p>
      </Segment>
    );
  }

  const liked = getValue(userInterests, 'liked', 'Liked', []) || [];
  const hated = getValue(userInterests, 'hated', 'Hated', []) || [];

  return (
    <Segment loading={loading}>
      <Header as="h4">
        <Icon name="compass outline" />
        <Header.Content>Soulseek Native Discovery</Header.Content>
      </Header>

      <Form>
        <Form.Group widths="equal">
          <Form.Input
            label="Interest"
            onChange={(event) => setInterest(event.target.value)}
            placeholder="genre, artist, scene, tag"
            value={interest}
          />
          <Form.Input
            label="Item"
            onChange={(event) => setItem(event.target.value)}
            placeholder="item to branch from"
            value={item}
          />
          <Form.Input
            label="User"
            onChange={(event) => setUsername(event.target.value)}
            placeholder="username"
            value={username}
          />
        </Form.Group>
        <Button.Group size="small">
          <Button
            icon
            labelPosition="left"
            onClick={() => updateInterest(false, false)}
            type="button"
          >
            <Icon name="heart" />
            Add Interest
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={() => updateInterest(true, false)}
            type="button"
          >
            <Icon name="ban" />
            Add Hated
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={() => updateInterest(false, true)}
            type="button"
          >
            <Icon name="minus circle" />
            Remove Interest
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={() => updateInterest(true, true)}
            type="button"
          >
            <Icon name="minus square outline" />
            Remove Hated
          </Button>
        </Button.Group>
        <Button.Group
          floated="right"
          size="small"
        >
          <Button
            icon
            labelPosition="left"
            onClick={() => loadRecommendations(false)}
            type="button"
          >
            <Icon name="lightbulb outline" />
            My Recs
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={() => loadRecommendations(true)}
            type="button"
          >
            <Icon name="globe" />
            Global
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={loadSimilarUsers}
            type="button"
          >
            <Icon name="users" />
            Similar Users
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={loadItemRecommendations}
            type="button"
          >
            <Icon name="sitemap" />
            Item Recs
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={loadItemSimilarUsers}
            type="button"
          >
            <Icon name="user plus" />
            Item Users
          </Button>
          <Button
            icon
            labelPosition="left"
            onClick={() => loadUserInterests()}
            type="button"
          >
            <Icon name="address card outline" />
            User Interests
          </Button>
        </Button.Group>
      </Form>

      {error && (
        <Message
          content={error}
          error
        />
      )}
      {status && (
        <Message
          content={status}
          positive={!error}
        />
      )}

      {hasResults && (
        <Segment secondary>
          <Header as="h5">{title || 'Discovery results'}</Header>
          {recommendations.length > 0 && (
            <List
              divided
              relaxed
            >
              {recommendations.map(renderRecommendation)}
            </List>
          )}
          {unrecommendations.length > 0 && (
            <>
              <Header as="h5">Unrecommendations</Header>
              <List
                divided
                relaxed
              >
                {unrecommendations.map(renderRecommendation)}
              </List>
            </>
          )}
          {similarUsers.length > 0 && (
            <List
              divided
              relaxed
            >
              {similarUsers.map(renderSimilarUser)}
            </List>
          )}
          {userInterests && (
            <>
              {renderInterestList('Liked', liked, 'green')}
              {renderInterestList('Hated', hated, 'red')}
            </>
          )}
        </Segment>
      )}
    </Segment>
  );
};

export default SoulseekDiscoveryPanel;
