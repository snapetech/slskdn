import './DiscoveryInbox.css';
import { getAcquisitionProfile } from '../../lib/acquisitionProfiles';
import {
  bulkUpdateDiscoveryInboxItems,
  getDiscoveryInboxItems,
  updateDiscoveryInboxItemState,
} from '../../lib/discoveryInbox';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Header,
  Icon,
  Label,
  Popup,
  Segment,
} from 'semantic-ui-react';

const stateColors = {
  Approved: 'green',
  Downloading: 'blue',
  Failed: 'red',
  Imported: 'teal',
  Rejected: 'red',
  Snoozed: 'grey',
  Staged: 'violet',
  Suggested: 'yellow',
};

const formatTimestamp = (timestamp) => {
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? 'Unknown time' : date.toLocaleString();
};

const DiscoveryInbox = () => {
  const [items, setItems] = useState([]);

  const refreshItems = () => {
    setItems(getDiscoveryInboxItems());
  };

  useEffect(() => {
    refreshItems();
  }, []);

  const suggestedIds = useMemo(
    () => items.filter((item) => item.state === 'Suggested').map((item) => item.id),
    [items],
  );

  const stateCounts = useMemo(
    () =>
      items.reduce(
        (counts, item) => ({
          ...counts,
          [item.state]: (counts[item.state] || 0) + 1,
        }),
        {},
      ),
    [items],
  );

  const setItemState = (item, state) => {
    setItems(updateDiscoveryInboxItemState(item.id, state));
  };

  const bulkSetSuggested = (state) => {
    setItems(bulkUpdateDiscoveryInboxItems(suggestedIds, state));
  };

  return (
    <Segment
      className="discovery-inbox"
      raised
    >
      <div className="discovery-inbox-header">
        <Header as="h2">
          <Icon name="inbox" />
          <Header.Content>
            Discovery Inbox
            <Header.Subheader>
              Review acquisition candidates before any network activity starts.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <div className="discovery-inbox-actions">
          <Popup
            content="Approve every currently suggested item. This marks the candidates ready for the next acquisition step, but this screen does not start downloads."
            position="top center"
            trigger={
              <Button
                aria-label="Approve suggested discovery items"
                disabled={suggestedIds.length === 0}
                onClick={() => bulkSetSuggested('Approved')}
                positive
              >
                <Icon name="check" />
                Approve Suggested
              </Button>
            }
          />
          <Popup
            content="Reject every currently suggested item so the same evidence does not stay in the review queue."
            position="top center"
            trigger={
              <Button
                aria-label="Reject suggested discovery items"
                disabled={suggestedIds.length === 0}
                negative
                onClick={() => bulkSetSuggested('Rejected')}
              >
                <Icon name="ban" />
                Reject Suggested
              </Button>
            }
          />
        </div>
      </div>

      <div className="discovery-inbox-summary">
        {['Suggested', 'Approved', 'Snoozed', 'Rejected'].map((state) => (
          <Label
            color={stateColors[state]}
            key={state}
          >
            {state}
            <Label.Detail>{stateCounts[state] || 0}</Label.Detail>
          </Label>
        ))}
      </div>

      {items.length === 0 ? (
        <Segment
          className="discovery-inbox-empty"
          placeholder
        >
          <Header icon>
            <Icon name="inbox" />
            No discovery candidates yet
          </Header>
          <p>
            Save a search phrase into the inbox from Search to review it before
            it becomes download work.
          </p>
        </Segment>
      ) : (
        <div className="discovery-inbox-grid">
          {items.map((item) => {
            const profile = getAcquisitionProfile(item.acquisitionProfile);

            return (
              <Segment
                className="discovery-inbox-card"
                key={item.id}
              >
                <div className="discovery-inbox-card-head">
                  <div>
                    <div className="discovery-inbox-title">{item.title}</div>
                    <div className="discovery-inbox-meta">
                      <Icon name="clock outline" />
                      {formatTimestamp(item.createdAt)}
                      <span> · </span>
                      <Icon name={profile.icon} />
                      {profile.label}
                      <span> · </span>
                      <Icon name="map signs" />
                      {item.source}
                    </div>
                  </div>
                  <Label color={stateColors[item.state] || 'grey'}>
                    {item.state}
                  </Label>
                </div>
                <div className="discovery-inbox-reason">
                  <strong>Why:</strong> {item.reason}
                </div>
                <div className="discovery-inbox-impact">
                  <strong>Network impact:</strong> {item.networkImpact}
                </div>
                <div className="discovery-inbox-card-actions">
                  <Popup
                    content="Mark this candidate approved for a later acquisition step. No download starts from this button."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Approve ${item.title}`}
                        disabled={item.state === 'Approved'}
                        onClick={() => setItemState(item, 'Approved')}
                        positive
                        size="small"
                      >
                        <Icon name="check" />
                        Approve
                      </Button>
                    }
                  />
                  <Popup
                    content="Snooze this candidate so it stays out of the immediate review set without losing the evidence."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Snooze ${item.title}`}
                        disabled={item.state === 'Snoozed'}
                        onClick={() => setItemState(item, 'Snoozed')}
                        size="small"
                      >
                        <Icon name="clock" />
                        Snooze
                      </Button>
                    }
                  />
                  <Popup
                    content="Reject this candidate and keep that decision with the saved evidence."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Reject ${item.title}`}
                        disabled={item.state === 'Rejected'}
                        negative
                        onClick={() => setItemState(item, 'Rejected')}
                        size="small"
                      >
                        <Icon name="ban" />
                        Reject
                      </Button>
                    }
                  />
                </div>
              </Segment>
            );
          })}
        </div>
      )}
    </Segment>
  );
};

export default DiscoveryInbox;
