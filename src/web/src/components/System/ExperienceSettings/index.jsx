import React, { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Header,
  Icon,
  Message,
  Popup,
  Segment,
} from 'semantic-ui-react';

const storageKey = 'slskdn:experience-preferences:v1';

const defaults = {
  discoveryApprovalFilter: 'all',
  discoveryConfidenceFloor: '0.70',
  discoveryExplanationDetail: 'full',
  discoveryProviderFilter: '',
  discoveryStaleDays: '14',
  messagesDenseMode: false,
  messagesPinnedRestore: true,
  messagesRoomUserFilter: true,
  messagesSearchEnabled: true,
  messagesUnreadBadges: true,
  playerCaptureHistory: true,
  playerDefaultVisualizer: 'last',
  playerKeyboardShortcuts: true,
  playerQueueAutoFill: false,
  playerRadioSeedMode: 'current',
  playerScrobbleMode: 'manual',
  playerShowRatings: true,
  searchActionPreviewDensity: 'detailed',
  searchDuplicateFolding: true,
  searchPreferredCondition: 'lossless',
  searchRankingProfile: 'balanced',
};

const options = {
  actionPreviewDensity: [
    { key: 'compact', text: 'Compact', value: 'compact' },
    { key: 'detailed', text: 'Detailed', value: 'detailed' },
  ],
  approvalFilter: [
    { key: 'all', text: 'All', value: 'all' },
    { key: 'needs-review', text: 'Needs review', value: 'needs-review' },
    { key: 'approved', text: 'Approved', value: 'approved' },
    { key: 'snoozed', text: 'Snoozed', value: 'snoozed' },
  ],
  explanationDetail: [
    { key: 'compact', text: 'Compact', value: 'compact' },
    { key: 'full', text: 'Full evidence', value: 'full' },
  ],
  preferredCondition: [
    { key: 'lossless', text: 'Lossless first', value: 'lossless' },
    { key: 'complete', text: 'Complete releases first', value: 'complete' },
    { key: 'fastest', text: 'Fastest source first', value: 'fastest' },
  ],
  radioSeedMode: [
    { key: 'current', text: 'Current track', value: 'current' },
    { key: 'queue', text: 'Queue context', value: 'queue' },
    { key: 'history', text: 'Listening history', value: 'history' },
  ],
  rankingProfile: [
    { key: 'balanced', text: 'Balanced', value: 'balanced' },
    { key: 'quality', text: 'Quality', value: 'quality' },
    { key: 'availability', text: 'Availability', value: 'availability' },
    { key: 'network-light', text: 'Network-light', value: 'network-light' },
  ],
  scrobbleMode: [
    { key: 'manual', text: 'Manual', value: 'manual' },
    { key: 'review', text: 'Review before send', value: 'review' },
    { key: 'off', text: 'Off', value: 'off' },
  ],
  visualizerDefault: [
    { key: 'last', text: 'Last used', value: 'last' },
    { key: 'art', text: 'Album art', value: 'art' },
    { key: 'butterchurn', text: 'Butterchurn', value: 'butterchurn' },
    { key: 'native-webgl2', text: 'MilkDrop3 WebGL2', value: 'native-webgl2' },
    { key: 'native-webgpu', text: 'MilkDrop3 WebGPU', value: 'native-webgpu' },
  ],
};

const readStoredPreferences = () => {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) || '{}');
    return { ...defaults, ...stored };
  } catch {
    return defaults;
  }
};

const buildReport = (form) =>
  [
    'slskdN experience preferences',
    `Search: ranking=${form.searchRankingProfile}, condition=${form.searchPreferredCondition}, duplicate_folding=${form.searchDuplicateFolding}, previews=${form.searchActionPreviewDensity}`,
    `Discovery: provider=${form.discoveryProviderFilter || 'all'}, approval=${form.discoveryApprovalFilter}, confidence>=${form.discoveryConfidenceFloor}, stale_days=${form.discoveryStaleDays}, explanations=${form.discoveryExplanationDetail}`,
    `Player: queue_auto_fill=${form.playerQueueAutoFill}, radio_seed=${form.playerRadioSeedMode}, ratings=${form.playerShowRatings}, history=${form.playerCaptureHistory}, scrobble=${form.playerScrobbleMode}, visualizer=${form.playerDefaultVisualizer}, shortcuts=${form.playerKeyboardShortcuts}`,
    `Messages: dense=${form.messagesDenseMode}, pinned_restore=${form.messagesPinnedRestore}, unread_badges=${form.messagesUnreadBadges}, user_filter=${form.messagesRoomUserFilter}, search=${form.messagesSearchEnabled}`,
  ].join('\n');

const ExperienceSettings = () => {
  const [form, setForm] = useState(defaults);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    setForm(readStoredPreferences());
  }, []);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
    setMessage(null);
  };

  const save = () => {
    localStorage.setItem(storageKey, JSON.stringify(form));
    setMessage('Experience preferences saved locally in this browser.');
  };

  const reset = () => {
    localStorage.removeItem(storageKey);
    setForm(defaults);
    setMessage('Experience preferences reset to defaults.');
  };

  const copyReport = () => {
    navigator.clipboard?.writeText(buildReport(form));
    setMessage('Experience preference report copied.');
  };

  return (
    <div className="experience-settings">
      <Segment>
        <Header as="h3">
          <Icon name="compass" />
          Experience Preferences
        </Header>
        <p>
          Browser-local preference surface for Search, Player, and Messages
          behavior that can be consumed by page-specific backfill.
        </p>
      </Segment>

      {message && (
        <Message
          positive
          size="small"
        >
          {message}
        </Message>
      )}

      <Card.Group
        itemsPerRow={1}
        stackable
      >
        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="search" />
              Search
            </Card.Header>
            <Card.Meta>Ranking, duplicate folding, preferred conditions, and planned-action density.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <Form>
              <Form.Group widths="equal">
                <Form.Select
                  aria-label="Search ranking profile preference"
                  label="Ranking Profile"
                  onChange={(_, { value }) => update('searchRankingProfile', value)}
                  options={options.rankingProfile}
                  value={form.searchRankingProfile}
                />
                <Form.Select
                  aria-label="Search preferred condition preference"
                  label="Preferred Condition"
                  onChange={(_, { value }) => update('searchPreferredCondition', value)}
                  options={options.preferredCondition}
                  value={form.searchPreferredCondition}
                />
                <Form.Select
                  aria-label="Search action preview density preference"
                  label="Action Preview Density"
                  onChange={(_, { value }) =>
                    update('searchActionPreviewDensity', value)
                  }
                  options={options.actionPreviewDensity}
                  value={form.searchActionPreviewDensity}
                />
              </Form.Group>
              <Popup
                content="Fold likely duplicate results into a single review row when page support is available."
                trigger={
                  <Checkbox
                    aria-label="Enable search duplicate folding preference"
                    checked={form.searchDuplicateFolding}
                    label="Fold duplicate results"
                    onChange={(_, { checked }) =>
                      update('searchDuplicateFolding', Boolean(checked))
                    }
                    toggle
                  />
                }
              />
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="play circle" />
              Player
            </Card.Header>
            <Card.Meta>Queue, radio, ratings, history, scrobbling, visualizer, and keyboard behavior.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <Form>
              <Form.Group widths="equal">
                <Form.Select
                  aria-label="Player radio seed preference"
                  label="Radio Seed"
                  onChange={(_, { value }) => update('playerRadioSeedMode', value)}
                  options={options.radioSeedMode}
                  value={form.playerRadioSeedMode}
                />
                <Form.Select
                  aria-label="Player scrobble mode preference"
                  label="Scrobble Mode"
                  onChange={(_, { value }) => update('playerScrobbleMode', value)}
                  options={options.scrobbleMode}
                  value={form.playerScrobbleMode}
                />
                <Form.Select
                  aria-label="Player default visualizer preference"
                  label="Default Visualizer"
                  onChange={(_, { value }) => update('playerDefaultVisualizer', value)}
                  options={options.visualizerDefault}
                  value={form.playerDefaultVisualizer}
                />
              </Form.Group>
              <Form.Group grouped>
                <Popup
                  content="Allow player pages to append local similar-track queue candidates when explicit page support is available."
                  trigger={
                    <Checkbox
                      aria-label="Enable player queue auto-fill preference"
                      checked={form.playerQueueAutoFill}
                      label="Enable queue auto-fill"
                      onChange={(_, { checked }) =>
                        update('playerQueueAutoFill', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Checkbox
                  aria-label="Show player ratings preference"
                  checked={form.playerShowRatings}
                  label="Show ratings"
                  onChange={(_, { checked }) =>
                    update('playerShowRatings', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Capture player history preference"
                  checked={form.playerCaptureHistory}
                  label="Capture local history"
                  onChange={(_, { checked }) =>
                    update('playerCaptureHistory', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Enable player keyboard shortcuts preference"
                  checked={form.playerKeyboardShortcuts}
                  label="Enable keyboard shortcuts"
                  onChange={(_, { checked }) =>
                    update('playerKeyboardShortcuts', Boolean(checked))
                  }
                />
              </Form.Group>
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="comments" />
              Messages
            </Card.Header>
            <Card.Meta>Dense display, pinned panels, unread badges, user filtering, and local search preference.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <Form>
              <Form.Group grouped>
                <Checkbox
                  aria-label="Enable messages dense mode preference"
                  checked={form.messagesDenseMode}
                  label="Dense display"
                  onChange={(_, { checked }) =>
                    update('messagesDenseMode', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Restore pinned message panels preference"
                  checked={form.messagesPinnedRestore}
                  label="Restore pinned panels"
                  onChange={(_, { checked }) =>
                    update('messagesPinnedRestore', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Show unread message badges preference"
                  checked={form.messagesUnreadBadges}
                  label="Show unread badges"
                  onChange={(_, { checked }) =>
                    update('messagesUnreadBadges', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Enable room user filtering preference"
                  checked={form.messagesRoomUserFilter}
                  label="Enable room user filtering"
                  onChange={(_, { checked }) =>
                    update('messagesRoomUserFilter', Boolean(checked))
                  }
                />
                <Checkbox
                  aria-label="Enable local message search preference"
                  checked={form.messagesSearchEnabled}
                  label="Enable local message search"
                  onChange={(_, { checked }) =>
                    update('messagesSearchEnabled', Boolean(checked))
                  }
                />
              </Form.Group>
            </Form>
          </Card.Content>
        </Card>
      </Card.Group>

      <div className="integration-actions">
        <Popup
          content="Save these preference choices to this browser. No server settings, searches, downloads, messages, or files are changed."
          trigger={
            <Button
              icon
              labelPosition="left"
              onClick={save}
              primary
            >
              <Icon name="save" />
              Save Local Preferences
            </Button>
          }
        />
        <Popup
          content="Reset these browser-local preference choices to the slskdN defaults."
          trigger={
            <Button
              icon
              labelPosition="left"
              onClick={reset}
            >
              <Icon name="undo" />
              Reset
            </Button>
          }
        />
        <Popup
          content="Copy a review report of the selected preferences for implementation handoff."
          trigger={
            <Button
              icon
              labelPosition="left"
              onClick={copyReport}
            >
              <Icon name="copy" />
              Copy Report
            </Button>
          }
        />
      </div>
    </div>
  );
};

export default ExperienceSettings;
