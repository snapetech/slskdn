import './Player.css';
import * as collectionsAPI from '../../lib/collections';
import {
  clearDiscoveryShelf,
  getDiscoveryShelf,
  getDiscoveryShelfActionLabel,
  getDiscoveryShelfSummary,
  removeDiscoveryShelfItem,
  upsertDiscoveryShelfItem,
} from '../../lib/discoveryShelf';
import * as externalVisualizer from '../../lib/externalVisualizer';
import * as listenBrainz from '../../lib/listenBrainz';
import {
  clearListeningHistory,
  exportListeningHistoryCsv,
  exportListeningHistoryJson,
  getListeningRecommendationSeeds,
  getListeningStats,
  importListeningHistory,
  recordLocalPlay,
} from '../../lib/listeningHistory';
import {
  getPlayerRating,
  getPlayerRatingSummary,
  setPlayerRating,
} from '../../lib/playerRatings';
import {
  buildPlayerRadioPlan,
  buildPlayerRadioSearchPath,
  getPlayerRadioCopyText,
} from '../../lib/playerRadio';
import { buildSimilarQueueCandidates } from '../../lib/playerAutoQueue';
import { getPlayerShortcutAction } from '../../lib/playerShortcuts';
import { getLocalStorageItem, setLocalStorageItem } from '../../lib/storage';
import * as streaming from '../../lib/streaming';
import Equalizer from './Equalizer';
import LyricsPane from './LyricsPane';
import SpectrumAnalyzer, { getFrequencyBars } from './SpectrumAnalyzer';
import { fadeOutputGain, resumeAudioGraph, setKaraokeEnabled, setOutputGain } from './audioGraph';
import { usePlayer } from './PlayerContext';
import Visualizer from './Visualizer';
import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Header,
  Icon,
  Input,
  Label,
  Message,
  Modal,
  Popup,
  Segment,
  Table,
  TextArea,
} from 'semantic-ui-react';

const localMuteStorageKey = 'slskdn.player.localMuted';
const collapsedStorageKey = 'slskdn.player.collapsed';
const visualizerStorageKey = 'slskdn.player.visualizerEnabled';
const eqPanelStorageKey = 'slskdn.player.eqPanelOpen';
const lyricsStorageKey = 'slskdn.player.lyricsOpen';
const karaokeStorageKey = 'slskdn.player.karaokeEnabled';
const crossfadeStorageKey = 'slskdn.player.crossfadeEnabled';
const visualTileStorageKey = 'slskdn.player.visualTileMode';
const analyzerModeStorageKey = 'slskdn.player.analyzerMode';
const playerBrowserPageSize = 80;

const readStoredBoolean = (key) => {
  return getLocalStorageItem(key) === 'true';
};

const readStoredTileMode = () => {
  const mode = getLocalStorageItem(visualTileStorageKey);
  return mode === 'milkdrop' ? 'milkdrop' : 'art';
};

const readStoredAnalyzerMode = () => {
  const mode = getLocalStorageItem(analyzerModeStorageKey);
  return mode === 'scope' ? 'scope' : 'spectrum';
};

const setPlayerHeightVariable = (element) => {
  if (!element || typeof document === 'undefined') return;

  const height = Math.ceil(element.getBoundingClientRect().height);
  if (height > 0) {
    document.documentElement.style.setProperty(
      '--slskdn-player-reserved-height',
      `${height}px`,
    );
  }
};

const getExternalVisualizerStatusText = (status, loading) => {
  if (loading) return 'Checking external visualizer launcher...';
  if (!status) return 'Status unavailable.';
  if (!status.enabled) return 'Disabled in slskd.yml.';
  if (!status.configured) return 'No launcher path configured.';
  if (!status.available) return 'Configured launcher path was not found.';
  return 'Ready to launch on the slskdN host.';
};

const getExternalVisualizerError = (error) => {
  const data = error?.response?.data;
  if (typeof data === 'string') return data;
  if (data?.error) return data.error;
  return 'External visualizer did not launch.';
};

const PlayerToolButton = ({
  active = false,
  children = null,
  content,
  disabled = false,
  icon,
  label,
  ...buttonProps
}) => (
  <Popup
    content={content}
    trigger={
      <Button
        {...buttonProps}
        className={[
          'player-tool-button',
          buttonProps.className,
          active ? 'player-tool-button-active' : '',
        ].filter(Boolean).join(' ')}
        disabled={disabled}
        icon={!label}
        size="small"
        type="button"
      >
        <Icon name={icon} />
        {label ? <span>{label}</span> : null}
        {children}
      </Button>
    }
  />
);

const formatPlayerProvider = (provider = '') => {
  const normalized = String(provider).trim();
  if (!normalized) return '';
  if (normalized.toLowerCase() === 'soulseek') return 'Soulseek';
  if (normalized.toLowerCase() === 'mesh') return 'Mesh';
  if (normalized.toLowerCase() === 'pod') return 'Pod';
  return normalized;
};

const getPlayerBadges = (current) => {
  if (!current) return [];

  const providers = (Array.isArray(current.sourceProviders)
    ? current.sourceProviders
    : [])
    .map(formatPlayerProvider)
    .filter(Boolean);
  const badges = providers.slice(0, 2).map((provider) => ({
    color: provider === 'Mesh' || provider === 'Pod' ? 'violet' : 'grey',
    icon: provider === 'Mesh' ? 'share alternate' : 'music',
    key: `source-${provider}`,
    text: provider,
    title: `Playback source: ${provider}`,
  }));

  const confidence = Number(current.confidence || 0);
  if (confidence > 0) {
    badges.push({
      color: confidence >= 0.75 ? 'green' : 'yellow',
      icon: 'crosshairs',
      key: 'confidence',
      text: `${Math.round(confidence * 100)}% match`,
      title: 'Local match confidence for this now-playing item.',
    });
  }

  if (current.verified) {
    badges.push({
      color: 'teal',
      icon: 'check circle',
      key: 'verified',
      text: 'Verified',
      title: 'This item has local verification evidence.',
    });
  }

  return badges;
};

const PlayerRatingControls = ({ current, onChange, rating }) => {
  if (!current) return null;

  const summary = getPlayerRatingSummary(current);

  return (
    <div
      aria-label="Now playing rating"
      className={[
        'player-rating-controls',
        `player-rating-controls-${summary.tone}`,
      ].join(' ')}
      data-testid="player-rating-controls"
      role="group"
    >
      {[1, 2, 3, 4, 5].map((value) => (
        <Popup
          content={
            value === rating
              ? 'Clear this local rating.'
              : `Rate this track ${value} out of 5 for local discovery context.`
          }
          key={value}
          trigger={
            <button
              aria-label={
                value === rating
                  ? `Clear ${value} star rating`
                  : `Rate ${value} stars`
              }
              className={[
                'player-rating-button',
                value <= rating ? 'player-rating-button-active' : '',
              ].filter(Boolean).join(' ')}
              data-testid={`player-rating-${value}`}
              onClick={() => onChange(value === rating ? 0 : value)}
              title={
                value === rating
                  ? 'Clear this local rating.'
                  : `Rate this track ${value} out of 5.`
              }
              type="button"
            >
              <Icon name={value <= rating ? 'star' : 'star outline'} />
            </button>
          }
        />
      ))}
      <span className="player-rating-summary">{summary.label}</span>
    </div>
  );
};

const PlayerRadioModal = ({ current, onClose, onOpenSearch, open }) => {
  const plan = buildPlayerRadioPlan(current);
  const copyText = getPlayerRadioCopyText(plan);

  const copyPlan = () => {
    if (navigator.clipboard && copyText) {
      navigator.clipboard.writeText(copyText).catch(() => {});
    }
  };

  return (
    <Modal
      className="player-browser-modal player-radio-modal"
      onClose={onClose}
      open={open}
      size="small"
    >
      <Modal.Header>Smart Radio Seed</Modal.Header>
      <Modal.Content>
        <p className="player-modal-copy">
          Build review-first radio searches from the current track. Nothing is
          searched or queued until you choose a query.
        </p>
        <div className="player-radio-seed" data-testid="player-radio-seed">
          <Icon name="random" />
          <div>
            <strong>{plan.seedLabel}</strong>
            <div>
              {plan.basis.length > 0 ? plan.basis.join(' | ') : 'Pick a track first.'}
            </div>
          </div>
        </div>
        <div className="player-radio-query-list">
          {plan.queries.map((item) => (
            <div className="player-radio-query" key={item.id}>
              <div>
                <Label color="violet" size="mini">
                  {item.reason}
                </Label>
                <code>{item.query}</code>
              </div>
              <Popup
                content="Open this as a normal Search page query. This is the point where network search work can begin."
                trigger={
                  <Button
                    data-testid={`player-radio-search-${item.id}`}
                    onClick={() => onOpenSearch(item.query)}
                    size="mini"
                    type="button"
                  >
                    <Icon name="search" />
                    Search
                  </Button>
                }
              />
            </div>
          ))}
        </div>
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Copy the generated radio search plan as plain text."
          trigger={
            <Button
              data-testid="player-radio-copy"
              disabled={!copyText}
              onClick={copyPlan}
              type="button"
            >
              <Icon name="copy outline" />
              Copy Plan
            </Button>
          }
        />
        <Popup
          content="Close smart radio without starting a search."
          trigger={
            <Button
              data-testid="player-radio-close"
              onClick={onClose}
              primary
              type="button"
            >
              <Icon name="check" />
              Done
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const getTrackLabel = (item) =>
  item?.title || item?.fileName || item?.contentId || 'Untitled track';

const PlayerQueueModal = ({
  current,
  history,
  onClearQueue,
  onAutoQueueSimilar,
  onClose,
  onNext,
  onPrevious,
  onRemove,
  open,
  queue,
}) => {
  const upcoming = queue.slice(1);
  const similarCandidates = buildSimilarQueueCandidates({
    current,
    history,
    queue,
  });

  return (
    <Modal
      className="player-browser-modal player-queue-modal"
      onClose={onClose}
      open={open}
      size="small"
    >
      <Modal.Header>Playback Queue</Modal.Header>
      <Modal.Content>
        <div className="player-queue-manager">
          <section>
            <div className="player-panel-title">Now Playing</div>
            <div className="player-queue-manager-row player-queue-manager-current">
              <Icon name="play circle outline" />
              <div>
                <strong>{getTrackLabel(current)}</strong>
                <span>{current?.artist || 'No active playback'}</span>
              </div>
            </div>
          </section>
          <section>
            <div className="player-queue-manager-heading">
              <div className="player-panel-title">Upcoming</div>
              <div className="player-queue-manager-actions">
                <Popup
                  content="Add similar recent session tracks to the upcoming queue. This only uses tracks already known to this browser session."
                  trigger={
                    <Button
                      data-testid="player-auto-queue-similar"
                      disabled={similarCandidates.length === 0}
                      onClick={() =>
                        onAutoQueueSimilar(
                          similarCandidates.map((candidate) => candidate.item),
                        )
                      }
                      size="mini"
                      type="button"
                    >
                      <Icon name="magic" />
                      Auto-fill Similar
                    </Button>
                  }
                />
                <Popup
                  content="Remove every upcoming item while keeping the current track playing."
                  trigger={
                    <Button
                      data-testid="player-clear-upcoming"
                      disabled={upcoming.length === 0}
                      onClick={onClearQueue}
                      size="mini"
                      type="button"
                    >
                      <Icon name="trash alternate outline" />
                      Clear Upcoming
                    </Button>
                  }
                />
              </div>
            </div>
            {upcoming.length > 0 ? (
              <div className="player-queue-manager-list">
                {upcoming.map((item, index) => (
                  <div
                    className="player-queue-manager-row"
                    data-testid={`player-queue-row-${item.contentId}`}
                    key={`${item.contentId}-${index}`}
                  >
                    <span className="player-queue-manager-index">{index + 1}</span>
                    <div>
                      <strong>{getTrackLabel(item)}</strong>
                      <span>{item.artist || item.album || item.contentId}</span>
                    </div>
                    <Popup
                      content="Remove this upcoming item from the local playback queue."
                      trigger={
                        <Button
                          aria-label={`Remove ${getTrackLabel(item)} from queue`}
                          data-testid={`player-remove-queue-${item.contentId}`}
                          icon
                          onClick={() => onRemove(item.contentId)}
                          size="mini"
                          type="button"
                        >
                          <Icon name="close" />
                        </Button>
                      }
                    />
                  </div>
                ))}
              </div>
            ) : (
              <div className="player-queue-manager-empty">
                No upcoming tracks.
              </div>
            )}
          </section>
          <section>
            <div className="player-panel-title">Recent</div>
            {history.length > 0 ? (
              <div className="player-queue-manager-list">
                {history.slice(0, 5).map((item, index) => (
                  <div
                    className="player-queue-manager-row"
                    key={`${item.contentId}-${index}`}
                  >
                    <Icon name="history" />
                    <div>
                      <strong>{getTrackLabel(item)}</strong>
                      <span>{item.artist || item.album || item.contentId}</span>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="player-queue-manager-empty">
                No recent tracks in this session.
              </div>
            )}
          </section>
        </div>
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Jump back to the previous session track, or restart the current track if there is no history."
          trigger={
            <Button
              data-testid="player-queue-previous"
              disabled={!current}
              onClick={onPrevious}
              type="button"
            >
              <Icon name="step backward" />
              Previous
            </Button>
          }
        />
        <Popup
          content="Advance to the next queued track."
          trigger={
            <Button
              data-testid="player-queue-next"
              disabled={queue.length < 2}
              onClick={onNext}
              type="button"
            >
              <Icon name="step forward" />
              Next
            </Button>
          }
        />
        <Popup
          content="Close the queue manager."
          trigger={
            <Button
              data-testid="player-queue-close"
              onClick={onClose}
              primary
              type="button"
            >
              <Icon name="check" />
              Done
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const PlayerDiscoveryShelfModal = ({ onClose, open }) => {
  const [items, setItems] = useState(() => getDiscoveryShelf());
  const [message, setMessage] = useState('');
  const summary = getDiscoveryShelfSummary();

  const refreshShelf = () => {
    setItems(getDiscoveryShelf());
  };

  useEffect(() => {
    if (open) {
      refreshShelf();
      setMessage('');
    }
  }, [open]);

  const previewAction = (item) => {
    setMessage(
      `${getDiscoveryShelfActionLabel(item.action)} prepared for ${item.title}. No files were moved or deleted.`,
    );
  };

  const removeItem = (key) => {
    removeDiscoveryShelfItem(key);
    refreshShelf();
  };

  const clearShelf = () => {
    clearDiscoveryShelf();
    refreshShelf();
    setMessage('Discovery shelf cleared from this browser.');
  };

  return (
    <Modal
      className="player-browser-modal player-discovery-shelf-modal"
      onClose={onClose}
      open={open}
      size="small"
    >
      <Modal.Header>Discovery Shelf</Modal.Header>
      <Modal.Content>
        <div className="player-shelf-summary" data-testid="player-shelf-summary">
          <div>
            <strong>{summary.total}</strong>
            <span>local review items</span>
          </div>
          <div>
            <strong>{summary['promote-preview']}</strong>
            <span>promote previews</span>
          </div>
          <div>
            <strong>{summary['archive-preview']}</strong>
            <span>archive previews</span>
          </div>
          <div>
            <strong>{summary['expiry-watch']}</strong>
            <span>expiry watch</span>
          </div>
        </div>
        {message ? (
          <Message compact size="mini">
            {message}
          </Message>
        ) : null}
        <div className="player-shelf-list">
          {items.length > 0 ? items.map((item) => (
            <div
              className="player-shelf-row"
              data-testid={`player-shelf-row-${item.key}`}
              key={item.key}
            >
              <div className="player-shelf-rating">{item.rating || '-'}</div>
              <div className="player-shelf-track">
                <strong>{item.title}</strong>
                <span>
                  {[item.artist, item.album].filter(Boolean).join(' - ') || 'Local discovery item'}
                </span>
              </div>
              <Label size="mini">
                {getDiscoveryShelfActionLabel(item.action)}
              </Label>
              <Popup
                content="Preview the shelf action. This does not move, delete, share, download, or publish anything."
                trigger={
                  <Button
                    data-testid={`player-shelf-preview-${item.key}`}
                    icon
                    onClick={() => previewAction(item)}
                    size="mini"
                    type="button"
                  >
                    <Icon name="eye" />
                  </Button>
                }
              />
              <Popup
                content="Remove this local review item from the browser-only shelf."
                trigger={
                  <Button
                    data-testid={`player-shelf-remove-${item.key}`}
                    icon
                    onClick={() => removeItem(item.key)}
                    size="mini"
                    type="button"
                  >
                    <Icon name="trash alternate outline" />
                  </Button>
                }
              />
            </div>
          )) : (
            <div className="player-queue-manager-empty">
              Rate tracks in the player to build a local discovery review shelf.
            </div>
          )}
        </div>
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Clear browser-local discovery shelf review items. This does not affect files or ratings."
          trigger={
            <Button
              data-testid="player-clear-discovery-shelf"
              disabled={items.length === 0}
              onClick={clearShelf}
              type="button"
            >
              <Icon name="trash" />
              Clear Shelf
            </Button>
          }
        />
        <Popup
          content="Close the local discovery shelf."
          trigger={
            <Button
              data-testid="player-close-discovery-shelf"
              onClick={onClose}
              primary
              type="button"
            >
              <Icon name="check" />
              Done
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const PlayerStatsModal = ({ onClose, onOpenSearch, open }) => {
  const fileInputRef = useRef(null);
  const [rangeDays, setRangeDays] = useState(30);
  const [importText, setImportText] = useState('');
  const [importStatus, setImportStatus] = useState(null);
  const [stats, setStats] = useState(() =>
    getListeningStats({ rangeDays: 30 }),
  );
  const recommendationSeeds = getListeningRecommendationSeeds(stats);

  const refreshStats = useCallback((nextRangeDays = rangeDays) => {
    setStats(getListeningStats({ rangeDays: nextRangeDays }));
  }, [rangeDays]);

  useEffect(() => {
    if (open) refreshStats();
  }, [open, refreshStats]);

  const clearStats = () => {
    clearListeningHistory();
    setImportStatus(null);
    refreshStats();
  };

  const updateRange = (nextRangeDays) => {
    setRangeDays(nextRangeDays);
    refreshStats(nextRangeDays);
  };

  const importHistory = () => {
    const result = importListeningHistory(importText);
    setImportStatus(
      `${result.imported} imported, ${result.skipped} skipped as duplicates or incomplete rows.`,
    );
    setImportText('');
    refreshStats();
  };

  const copyHistory = (format) => {
    const content = format === 'csv'
      ? exportListeningHistoryCsv()
      : exportListeningHistoryJson();

    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(content).catch(() => {});
    }

    setImportStatus(`Prepared ${format.toUpperCase()} export for ${stats.history.length} plays.`);
  };

  const readImportFile = (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    file.text().then((content) => {
      setImportText(content);
      setImportStatus(`Loaded ${file.name} for review.`);
    }).catch(() => {
      setImportStatus(`Could not read ${file.name}.`);
    });
    event.target.value = '';
  };

  const renderList = (items, emptyText) => (
    items.length > 0 ? (
      <div className="player-stats-list">
        {items.map((item, index) => (
          <div className="player-stats-row" key={`${item.label || item.title}-${index}`}>
            <span>{index + 1}</span>
            <strong>{item.label || item.title}</strong>
            <em>{item.plays ? `${item.plays} plays` : item.artist || item.album || ''}</em>
          </div>
        ))}
      </div>
    ) : (
      <div className="player-queue-manager-empty">{emptyText}</div>
    )
  );

  return (
    <Modal
      className="player-browser-modal player-stats-modal"
      onClose={onClose}
      open={open}
      size="small"
    >
      <Modal.Header>Listening Stats</Modal.Header>
      <Modal.Content>
        <div className="player-stats-summary" data-testid="player-stats-summary">
          <Icon name="bar chart" />
          <div>
            <strong>{stats.totalPlays}</strong>
            <span>
              local plays recorded in this browser
              {rangeDays ? ` over ${rangeDays} days` : ' overall'}
            </span>
          </div>
        </div>
        <div className="player-stats-ranges" role="group" aria-label="Listening stats range">
          {[
            { label: '7D', value: 7 },
            { label: '30D', value: 30 },
            { label: '90D', value: 90 },
            { label: 'All', value: null },
          ].map((range) => (
            <Button
              active={rangeDays === range.value}
              data-testid={`player-stats-range-${range.label}`}
              key={range.label}
              onClick={() => updateRange(range.value)}
              size="mini"
              type="button"
            >
              {range.label}
            </Button>
          ))}
        </div>
        <div className="player-stats-grid">
          <section>
            <div className="player-panel-title">Top Artists</div>
            {renderList(stats.topArtists, 'No artist plays recorded yet.')}
          </section>
          <section>
            <div className="player-panel-title">Top Tracks</div>
            {renderList(stats.topTracks, 'No track plays recorded yet.')}
          </section>
          <section>
            <div className="player-panel-title">Top Genres</div>
            {renderList(stats.topGenres, 'No genre metadata recorded yet.')}
          </section>
          <section>
            <div className="player-panel-title">Recent</div>
            {renderList(stats.recent, 'No recent plays recorded yet.')}
          </section>
          <section>
            <div className="player-panel-title">Forgotten Favorites</div>
            {renderList(
              stats.forgottenFavorites,
              'No older repeat plays outside this range yet.',
            )}
          </section>
        </div>
        <section className="player-stats-recommendations">
          <div className="player-panel-title">Recommendation Seeds</div>
          {recommendationSeeds.length > 0 ? (
            <div className="player-stats-seed-list">
              {recommendationSeeds.map((seed) => (
                <div className="player-stats-seed-row" key={`${seed.type}-${seed.query}`}>
                  <div>
                    <strong>{seed.label}</strong>
                    <span>{seed.type} - {seed.basis}</span>
                  </div>
                  <Popup
                    content="Open this local listening seed as a normal Search page query. Network search starts only after you choose to search."
                    trigger={
                      <Button
                        aria-label={`Search ${seed.label}`}
                        data-testid={`player-stats-search-seed-${seed.query}`}
                        icon
                        onClick={() => onOpenSearch(seed.query)}
                        size="mini"
                        type="button"
                      >
                        <Icon name="search" />
                      </Button>
                    }
                  />
                </div>
              ))}
            </div>
          ) : (
            <div className="player-queue-manager-empty">
              Play more tracks locally to build recommendation seeds.
            </div>
          )}
        </section>
        <section className="player-stats-import">
          <div className="player-panel-title">Media Server Import</div>
          <TextArea
            aria-label="Paste exported media server play history"
            data-testid="player-listening-history-import-text"
            onChange={(event) => setImportText(event.target.value)}
            placeholder="Paste Plex, Jellyfin, Navidrome, or generic CSV/JSON play history here for local import."
            rows={4}
            value={importText}
          />
          {importStatus ? (
            <Message compact size="mini">
              {importStatus}
            </Message>
          ) : null}
          <div className="player-stats-import-actions">
            <input
              accept=".csv,.json,.txt"
              aria-label="Choose media server history file"
              data-testid="player-listening-history-file"
              onChange={readImportFile}
              ref={fileInputRef}
              type="file"
            />
            <Popup
              content="Choose a local CSV or JSON export from Plex, Jellyfin, Navidrome, or another media server. The file is read in this browser only."
              trigger={
                <Button
                  data-testid="player-listening-history-choose-file"
                  onClick={() => fileInputRef.current?.click()}
                  size="mini"
                  type="button"
                >
                  <Icon name="folder open" />
                  Choose File
                </Button>
              }
            />
            <Popup
              content="Import the pasted or chosen play history into browser-local listening stats with duplicate suppression."
              trigger={
                <Button
                  data-testid="player-listening-history-import"
                  disabled={!importText.trim()}
                  onClick={importHistory}
                  primary
                  size="mini"
                  type="button"
                >
                  <Icon name="upload" />
                  Import
                </Button>
              }
            />
            <Popup
              content="Copy the browser-local listening history as JSON for backup or review."
              trigger={
                <Button
                  data-testid="player-listening-history-export-json"
                  disabled={stats.history.length === 0}
                  onClick={() => copyHistory('json')}
                  size="mini"
                  type="button"
                >
                  <Icon name="copy" />
                  JSON
                </Button>
              }
            />
            <Popup
              content="Copy the browser-local listening history as CSV for media-server or spreadsheet review."
              trigger={
                <Button
                  data-testid="player-listening-history-export-csv"
                  disabled={stats.history.length === 0}
                  onClick={() => copyHistory('csv')}
                  size="mini"
                  type="button"
                >
                  <Icon name="table" />
                  CSV
                </Button>
              }
            />
          </div>
        </section>
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Clear only the browser-local listening history used for this stats view."
          trigger={
            <Button
              data-testid="player-clear-listening-history"
              disabled={stats.totalPlays === 0}
              onClick={clearStats}
              type="button"
            >
              <Icon name="trash alternate outline" />
              Clear Local History
            </Button>
          }
        />
        <Popup
          content="Close listening stats."
          trigger={
            <Button
              data-testid="player-close-listening-stats"
              onClick={onClose}
              primary
              type="button"
            >
              <Icon name="check" />
              Done
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const PlayerLauncher = ({ compact = false, onPlayItem }) => {
  const navigate = useNavigate();
  const [collections, setCollections] = useState([]);
  const [collectionsOpen, setCollectionsOpen] = useState(false);
  const [selectedCollection, setSelectedCollection] = useState(null);
  const [collectionItems, setCollectionItems] = useState([]);
  const [collectionItemsLoading, setCollectionItemsLoading] = useState(false);
  const [items, setItems] = useState([]);
  const [browserDirectories, setBrowserDirectories] = useState([]);
  const [browserBreadcrumbs, setBrowserBreadcrumbs] = useState([]);
  const [browserHasMore, setBrowserHasMore] = useState(false);
  const [browserOffset, setBrowserOffset] = useState(0);
  const [browserPath, setBrowserPath] = useState('');
  const [browserStats, setBrowserStats] = useState({
    duplicatesRemoved: 0,
    totalDirectories: 0,
    totalFiles: 0,
  });
  const [filesOpen, setFilesOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [itemsLoading, setItemsLoading] = useState(false);

  useEffect(() => {
    let canceled = false;
    collectionsAPI
      .getCollections()
      .then((response) => {
        if (!canceled) setCollections(response.data || []);
      })
      .catch(() => {
        if (!canceled) setCollections([]);
      });

    return () => {
      canceled = true;
    };
  }, []);

  useEffect(() => {
    if (!filesOpen) return undefined;

    if (query && query.length < 2) {
      setItems([]);
      setBrowserDirectories([]);
      setBrowserBreadcrumbs([]);
      setBrowserHasMore(false);
      setBrowserStats({ duplicatesRemoved: 0, totalDirectories: 0, totalFiles: 0 });
      return undefined;
    }

    let canceled = false;
    const timeoutId = window.setTimeout(() => {
      setItemsLoading(true);
      collectionsAPI
        .browseLibraryItems({
          kinds: 'Audio',
          limit: playerBrowserPageSize,
          offset: browserOffset,
          path: browserPath,
          query,
        })
        .then((response) => {
          if (!canceled) {
            setItems(response.data?.files || []);
            setBrowserDirectories(response.data?.directories || []);
            setBrowserBreadcrumbs(response.data?.breadcrumbs || []);
            setBrowserHasMore(Boolean(response.data?.hasMore));
            setBrowserStats({
              duplicatesRemoved: response.data?.duplicatesRemoved || 0,
              totalDirectories: response.data?.totalDirectories || 0,
              totalFiles: response.data?.totalFiles || 0,
            });
          }
        })
        .catch(() => {
          if (!canceled) {
            setItems([]);
            setBrowserDirectories([]);
            setBrowserBreadcrumbs([]);
            setBrowserHasMore(false);
            setBrowserStats({ duplicatesRemoved: 0, totalDirectories: 0, totalFiles: 0 });
          }
        })
        .finally(() => {
          if (!canceled) setItemsLoading(false);
        });
    }, query ? 200 : 0);

    return () => {
      canceled = true;
      window.clearTimeout(timeoutId);
    };
  }, [browserOffset, browserPath, filesOpen, query]);

  const selectCollection = (collection) => {
    setSelectedCollection(collection);
    setCollectionItemsLoading(true);
    collectionsAPI
      .getCollectionItems(collection.id)
      .then((response) => setCollectionItems(response.data || []))
      .catch(() => setCollectionItems([]))
      .finally(() => setCollectionItemsLoading(false));
  };

  const playAndClose = (item) => {
    onPlayItem(item);
    setFilesOpen(false);
    setCollectionsOpen(false);
  };

  const openFileBrowser = () => {
    setBrowserOffset(0);
    setBrowserPath('');
    setFilesOpen(true);
    setQuery('');
  };

  const openBrowserPath = (path) => {
    setBrowserOffset(0);
    setBrowserPath(path || '');
    setQuery('');
  };

  const updateBrowserQuery = (value) => {
    setBrowserOffset(0);
    setQuery(value || '');
  };

  const shownFileCount = Math.min(
    browserOffset + items.length,
    browserStats.totalFiles,
  );

  return (
    <div className="player-launcher">
      <Popup
        content="Browse your collections and play an item from a playlist or share list."
        trigger={
          <Button
            aria-label="Open collections browser"
            className="player-library-button"
            compact
            data-testid="player-open-collections-browser"
            icon
            labelPosition={compact ? undefined : 'left'}
            onClick={() => setCollectionsOpen(true)}
            size="small"
            title="Open collections browser"
          >
            <Icon name="list" />
            {compact ? null : 'Collections'}
          </Button>
        }
      />
      <Popup
        content="Browse shared and downloaded local audio that slskdN can stream in this browser."
        trigger={
          <Button
            aria-label="Open local audio file browser"
            className="player-library-button"
            compact
            data-testid="player-open-file-browser"
            icon
            labelPosition={compact ? undefined : 'left'}
            onClick={openFileBrowser}
            size="small"
            title="Open local audio file browser"
          >
            <Icon name="folder open" />
            {compact ? null : 'Files'}
          </Button>
        }
      />

      <Modal
        className="player-browser-modal"
        data-testid="player-collection-browser-modal"
        onClose={() => setCollectionsOpen(false)}
        open={collectionsOpen}
        size="large"
      >
        <Modal.Header>Choose from Collections</Modal.Header>
        <Modal.Content>
          <div className="player-browser-grid">
            <Segment className="player-browser-panel">
              <Header as="h4">Collections</Header>
              {collections.length === 0 ? (
                <Message info>No collections found.</Message>
              ) : (
                <Table compact selectable>
                  <Table.Body>
                    {collections.map((collection) => (
                      <Table.Row
                        active={selectedCollection?.id === collection.id}
                        data-testid={`player-collection-row-${collection.id}`}
                        key={collection.id}
                        onClick={() => selectCollection(collection)}
                      >
                        <Table.Cell>
                          <strong>{collection.title}</strong>
                          <div className="player-picker-meta">
                            {collection.type || 'Playlist'}
                          </div>
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
              )}
            </Segment>
            <Segment className="player-browser-panel">
              <Header as="h4">
                {selectedCollection?.title || 'Collection Items'}
              </Header>
              {!selectedCollection ? (
                <Message info>Select a collection to see its tracks.</Message>
              ) : collectionItemsLoading ? (
                <Message info>Loading collection items...</Message>
              ) : collectionItems.length === 0 ? (
                <Message info>No playable items in this collection.</Message>
              ) : (
                <Table compact>
                  <Table.Header>
                    <Table.Row>
                      <Table.HeaderCell>Track</Table.HeaderCell>
                      <Table.HeaderCell collapsing>Action</Table.HeaderCell>
                    </Table.Row>
                  </Table.Header>
                  <Table.Body>
                    {collectionItems.map((item) => (
                      <Table.Row key={item.id || item.contentId}>
                        <Table.Cell>
                          <strong>
                            {item.fileName || item.title || item.contentId}
                          </strong>
                          <div className="player-picker-meta">
                            {item.mediaKind || 'Audio'}
                          </div>
                        </Table.Cell>
                        <Table.Cell collapsing>
                          <Popup
                            content="Play this collection item in the browser player."
                            trigger={
                              <Button
                                data-testid={`player-play-collection-item-${item.contentId}`}
                                icon
                                onClick={() => playAndClose(item)}
                                size="small"
                              >
                                <Icon name="play" />
                              </Button>
                            }
                          />
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
              )}
            </Segment>
          </div>
        </Modal.Content>
        <Modal.Actions>
          <Popup
            content="Open the full Collections page to create, edit, or share collections."
            trigger={
              <Button
                data-testid="player-manage-collections"
                onClick={() => {
                  setCollectionsOpen(false);
                  navigate('/collections');
                }}
              >
                <Icon name="external alternate" />
                Manage Collections
              </Button>
            }
          />
          <Popup
            content="Close the collection picker without changing playback."
            trigger={
              <Button onClick={() => setCollectionsOpen(false)}>Close</Button>
            }
          />
        </Modal.Actions>
      </Modal>

      <Modal
        className="player-browser-modal"
        data-testid="player-file-browser-modal"
        onClose={() => setFilesOpen(false)}
        open={filesOpen}
        size="fullscreen"
      >
        <Modal.Header>Browse Local Audio Library</Modal.Header>
        <Modal.Content>
          <div className="player-file-explorer">
            <div className="player-file-explorer-toolbar">
              <Input
                data-testid="player-file-browser-search"
                fluid
                icon="search"
                onChange={(_, { value }) => updateBrowserQuery(value)}
                placeholder="Search all audio by file, artist folder, album folder, or path"
                value={query}
              />
              <div className="player-file-explorer-counts">
                {itemsLoading
                  ? 'Loading...'
                  : `${shownFileCount} of ${browserStats.totalFiles} tracks`}
                {browserStats.duplicatesRemoved > 0
                  ? `, ${browserStats.duplicatesRemoved} duplicates collapsed`
                  : ''}
              </div>
            </div>

            <div className="player-file-explorer-breadcrumbs">
              {(browserBreadcrumbs.length > 0
                ? browserBreadcrumbs
                : [{ name: 'Library', path: '' }]).map((breadcrumb, index) => (
                  <React.Fragment key={breadcrumb.path || 'library'}>
                    {index > 0 ? <Icon name="angle right" /> : null}
                    <button
                      className="player-file-breadcrumb"
                      data-testid={`player-file-breadcrumb-${index}`}
                      onClick={() => openBrowserPath(breadcrumb.path)}
                      title={`Open ${breadcrumb.name}`}
                      type="button"
                    >
                      {breadcrumb.name}
                    </button>
                  </React.Fragment>
              ))}
            </div>

            <div className="player-file-explorer-body">
              <aside className="player-file-explorer-folders">
                <div className="player-file-explorer-section-title">
                  Folders
                </div>
                {query ? (
                  <Message info compact>
                    Clear search to browse folders.
                  </Message>
                ) : browserDirectories.length === 0 ? (
                  <Message info compact>
                    No child folders here.
                  </Message>
                ) : (
                  browserDirectories.map((directory) => (
                    <button
                      className="player-file-folder-row"
                      data-testid={`player-file-folder-${directory.path}`}
                      key={directory.path}
                      onClick={() => openBrowserPath(directory.path)}
                      title={`Open ${directory.name}`}
                      type="button"
                    >
                      <Icon name="folder" />
                      <span>
                        <strong>{directory.name}</strong>
                        <small>
                          {directory.fileCount} tracks
                          {directory.childDirectoryCount
                            ? `, ${directory.childDirectoryCount} folders`
                            : ''}
                        </small>
                      </span>
                    </button>
                  ))
                )}
              </aside>

              <section className="player-file-explorer-files">
                <div className="player-file-explorer-section-title">
                  {query ? 'Search Results' : browserPath || 'Library Root'}
                </div>
                {itemsLoading ? (
                  <Message info>Loading audio files...</Message>
                ) : items.length === 0 ? (
                  <Message info>
                    {query && query.length < 2
                      ? 'Type at least two characters to search.'
                      : 'No local audio files found here.'}
                  </Message>
                ) : (
                  <Table compact selectable>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Track</Table.HeaderCell>
                        <Table.HeaderCell>Location</Table.HeaderCell>
                        <Table.HeaderCell collapsing>Copies</Table.HeaderCell>
                        <Table.HeaderCell collapsing>Action</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {items.map((item) => (
                        <Table.Row
                          data-testid={`player-file-row-${item.contentId}`}
                          key={`${item.contentId}-${item.path}`}
                          onDoubleClick={() => playAndClose(item)}
                        >
                          <Table.Cell>
                            <strong>{item.fileName || item.contentId}</strong>
                            <div className="player-picker-meta">
                              {item.mediaKind || 'Audio'}
                              {item.bytes ? ` - ${Math.round(item.bytes / 1024 / 1024)} MB` : ''}
                            </div>
                          </Table.Cell>
                          <Table.Cell>
                            <span className="player-file-path">{item.path}</span>
                          </Table.Cell>
                          <Table.Cell collapsing>
                            {item.duplicateCount > 1 ? item.duplicateCount : ''}
                          </Table.Cell>
                          <Table.Cell collapsing>
                            <Popup
                              content="Play this local file in the browser player."
                              trigger={
                                <Button
                                  aria-label={`Play ${item.fileName || item.contentId}`}
                                  data-testid={`player-play-file-${item.contentId}`}
                                  icon
                                  onClick={() => playAndClose(item)}
                                  size="small"
                                  title={`Play ${item.fileName || item.contentId}`}
                                >
                                  <Icon name="play" />
                                </Button>
                              }
                            />
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table>
                )}
                <div className="player-file-explorer-pager">
                  <Popup
                    content="Move to the previous page of files in this folder or search."
                    trigger={
                      <Button
                        disabled={browserOffset === 0 || itemsLoading}
                        onClick={() =>
                          setBrowserOffset(Math.max(0, browserOffset - playerBrowserPageSize))
                        }
                        size="small"
                      >
                        <Icon name="angle left" />
                        Previous
                      </Button>
                    }
                  />
                  <Popup
                    content="Move to the next page of files in this folder or search."
                    trigger={
                      <Button
                        disabled={!browserHasMore || itemsLoading}
                        onClick={() =>
                          setBrowserOffset(browserOffset + playerBrowserPageSize)
                        }
                        size="small"
                      >
                        Next
                        <Icon name="angle right" />
                      </Button>
                    }
                  />
                </div>
              </section>
            </div>
          </div>
        </Modal.Content>
        <Modal.Actions>
          <Popup
            content="Close the local file browser without changing playback."
            trigger={<Button onClick={() => setFilesOpen(false)}>Close</Button>}
          />
        </Modal.Actions>
      </Modal>
    </div>
  );
};

const PlayerVisualTile = ({
  audioElement,
  current,
  mode,
  onModeChange,
  onTileModeChange,
  tileMode,
}) => {
  const title = current?.title || current?.fileName || 'slskdN';
  const artist = current?.artist || '';
  const initials = (artist || title)
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase() || 'N';
  const artworkUrl = current?.artworkUrl;
  const showingMilkdrop = tileMode === 'milkdrop' && mode !== 'off';
  const toggleTileMode = () => {
    if (showingMilkdrop) {
      onTileModeChange('art');
      return;
    }

    onTileModeChange('milkdrop');
    if (mode === 'off') onModeChange('inline');
  };

  return (
    <div className="player-visual-tile">
      <Popup
        content={
          showingMilkdrop
            ? 'Show album art in this square.'
            : 'Show MilkDrop in this square.'
        }
        trigger={
          <div
            aria-label={
              showingMilkdrop
                ? 'Show album art in player visual tile'
                : 'Show MilkDrop in player visual tile'
            }
            role="button"
            className="player-visual-stage"
            data-testid="player-visual-tile"
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                toggleTileMode();
              }
            }}
            onClick={toggleTileMode}
            tabIndex={0}
          >
            {showingMilkdrop ? (
              <Visualizer
                audioElement={audioElement}
                mode={mode}
                onModeChange={onModeChange}
              />
            ) : (
              <span className="player-album-art" data-testid="player-album-art">
                {artworkUrl ? (
                  <img alt="" src={artworkUrl} />
                ) : (
                  <>
                    <span className="player-album-art-glow" />
                    <span className="player-album-art-mark">{initials}</span>
                  </>
                )}
              </span>
            )}
            <span className="player-visual-affordance">
              <Icon name={showingMilkdrop ? 'image outline' : 'magic'} />
            </span>
          </div>
        }
      />
    </div>
  );
};

const PlayerAnalyzerTile = ({ audioElement, mode, onModeChange }) => {
  const nextMode = mode === 'spectrum' ? 'scope' : 'spectrum';
  const label = mode === 'spectrum' ? 'Spectrum bars' : 'Signal scope';

  return (
    <Popup
      content={
        mode === 'spectrum'
          ? 'Show signal scope in this box.'
          : 'Show spectrum bars in this box.'
      }
      trigger={
        <div
          aria-label={`Show ${nextMode === 'spectrum' ? 'spectrum bars' : 'signal scope'}`}
          className="player-analyzer-tile"
          data-testid="player-analyzer-tile"
          onClick={() => onModeChange(nextMode)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              onModeChange(nextMode);
            }
          }}
          role="button"
          tabIndex={0}
        >
          <div className="player-analyzer-label">{label}</div>
          <SpectrumAnalyzer
            audioElement={mode === 'off' ? null : audioElement}
            className="player-spectrum-switchable"
            mode={mode}
          />
          <span className="player-analyzer-affordance">
            <Icon name={mode === 'spectrum' ? 'signal' : 'chart bar'} />
          </span>
        </div>
      }
    />
  );
};

const PlayerBar = () => {
  const navigate = useNavigate();
  const audioRef = useRef(null);
  const fadeAudioRef = useRef(null);
  const lastSourceRef = useRef('');
  const playerBarRef = useRef(null);
  const scrobbledRef = useRef('');
  const pipRef = useRef({ raf: null, win: null });
  const {
    clearQueue,
    clear,
    current,
    followingParty,
    history,
    next,
    pause,
    queue,
    previous,
    queueItems,
    removeFromQueue,
    seekRelative,
    setAudioElement,
    playItem,
  } = usePlayer();
  const [localMuted, setLocalMuted] = useState(() =>
    readStoredBoolean(localMuteStorageKey),
  );
  const [collapsed, setCollapsed] = useState(() =>
    readStoredBoolean(collapsedStorageKey),
  );
  const [playing, setPlaying] = useState(false);
  const [visualizerMode, setVisualizerMode] = useState(() =>
    readStoredBoolean(visualizerStorageKey) ? 'inline' : 'off',
  );
  const [visualTileMode, setVisualTileMode] = useState(readStoredTileMode);
  const [analyzerMode, setAnalyzerMode] = useState(readStoredAnalyzerMode);
  const [eqPanelOpen, setEqPanelOpen] = useState(() =>
    readStoredBoolean(eqPanelStorageKey),
  );
  const [lyricsOpen, setLyricsOpen] = useState(() =>
    readStoredBoolean(lyricsStorageKey),
  );
  const [karaokeEnabled, setKaraokeEnabledState] = useState(() =>
    readStoredBoolean(karaokeStorageKey),
  );
  const [crossfadeEnabled, setCrossfadeEnabled] = useState(() =>
    readStoredBoolean(crossfadeStorageKey),
  );
  const [listenBrainzToken, setListenBrainzTokenState] = useState(() =>
    listenBrainz.getListenBrainzToken(),
  );
  const [integrationsOpen, setIntegrationsOpen] = useState(false);
  const [queueOpen, setQueueOpen] = useState(false);
  const [radioOpen, setRadioOpen] = useState(false);
  const [shelfOpen, setShelfOpen] = useState(false);
  const [statsOpen, setStatsOpen] = useState(false);
  const [externalVisualizerStatus, setExternalVisualizerStatus] = useState(null);
  const [externalVisualizerLoading, setExternalVisualizerLoading] = useState(false);
  const [externalVisualizerLaunching, setExternalVisualizerLaunching] = useState(false);
  const [externalVisualizerMessage, setExternalVisualizerMessage] = useState('');
  const [playerAudioElement, setPlayerAudioElement] = useState(null);
  const [playerRating, setPlayerRatingState] = useState(0);
  const [source, setSource] = useState('');

  const refreshExternalVisualizerStatus = useCallback(() => {
    setExternalVisualizerLoading(true);
    setExternalVisualizerMessage('');

    return externalVisualizer.getExternalVisualizerStatus()
      .then((status) => {
        setExternalVisualizerStatus(status);
        return status;
      })
      .catch(() => {
        setExternalVisualizerStatus(null);
        setExternalVisualizerMessage('External visualizer status is unavailable.');
      })
      .finally(() => {
        setExternalVisualizerLoading(false);
      });
  }, []);

  const launchExternalVisualizer = useCallback(() => {
    setExternalVisualizerLaunching(true);
    setExternalVisualizerMessage('');

    externalVisualizer.launchExternalVisualizer()
      .then((result) => {
        const name = result?.name || externalVisualizerStatus?.name || 'External visualizer';
        setExternalVisualizerMessage(
          result?.started ? `${name} launched.` : result?.error || 'External visualizer did not launch.',
        );
      })
      .catch((error) => {
        setExternalVisualizerMessage(getExternalVisualizerError(error));
      })
      .finally(() => {
        setExternalVisualizerLaunching(false);
      });
  }, [externalVisualizerStatus]);

  const bindAudioElement = useCallback((element) => {
    audioRef.current = element;
    setPlayerAudioElement(element);
    setAudioElement(element);
  }, [setAudioElement]);

  useLayoutEffect(() => {
    const element = playerBarRef.current;
    if (!element) return undefined;

    setPlayerHeightVariable(element);
    if (typeof window.ResizeObserver !== 'function') {
      return undefined;
    }

    const resizeObserver = new window.ResizeObserver(() =>
      setPlayerHeightVariable(element));
    resizeObserver.observe(element);

    return () => resizeObserver.disconnect();
  }, [collapsed, current, eqPanelOpen, lyricsOpen]);

  const playAudio = useCallback(async () => {
    if (!audioRef.current) return;
    await resumeAudioGraph(audioRef.current);
    await audioRef.current.play();
  }, []);

  useEffect(() => {
    if (!playerAudioElement) return;
    playerAudioElement.muted = localMuted;
    setLocalStorageItem(localMuteStorageKey, localMuted ? 'true' : 'false');
  }, [localMuted, playerAudioElement]);

  useEffect(() => {
    setLocalStorageItem(collapsedStorageKey, collapsed ? 'true' : 'false');
  }, [collapsed]);

  useEffect(() => {
    document.documentElement.classList.toggle('player-collapsed', collapsed);
    return () => {
      document.documentElement.classList.remove('player-collapsed');
    };
  }, [collapsed]);

  useEffect(() => {
    setLocalStorageItem(
      visualizerStorageKey,
      visualizerMode !== 'off' ? 'true' : 'false',
    );
  }, [visualizerMode]);

  useEffect(() => {
    setLocalStorageItem(visualTileStorageKey, visualTileMode);
  }, [visualTileMode]);

  useEffect(() => {
    setLocalStorageItem(analyzerModeStorageKey, analyzerMode);
  }, [analyzerMode]);

  useEffect(() => {
    if (integrationsOpen) {
      refreshExternalVisualizerStatus();
    }
  }, [integrationsOpen, refreshExternalVisualizerStatus]);

  useEffect(() => {
    setLocalStorageItem(eqPanelStorageKey, eqPanelOpen ? 'true' : 'false');
  }, [eqPanelOpen]);

  useEffect(() => {
    setLocalStorageItem(lyricsStorageKey, lyricsOpen ? 'true' : 'false');
  }, [lyricsOpen]);

  useEffect(() => {
    setLocalStorageItem(
      karaokeStorageKey,
      karaokeEnabled ? 'true' : 'false',
    );
    if (playerAudioElement) {
      setKaraokeEnabled(playerAudioElement, karaokeEnabled);
    }
  }, [karaokeEnabled, playerAudioElement]);

  useEffect(() => {
    setLocalStorageItem(
      crossfadeStorageKey,
      crossfadeEnabled ? 'true' : 'false',
    );
  }, [crossfadeEnabled]);

  const toggleVisualizer = () => {
    setVisualTileMode('milkdrop');
    setVisualizerMode((mode) => (mode === 'off' ? 'inline' : 'off'));
  };

  useEffect(() => {
    setPlayerRatingState(getPlayerRating(current));
  }, [current]);

  const updatePlayerRating = (rating) => {
    const nextRating = setPlayerRating(current, rating);
    setPlayerRatingState(nextRating);
    upsertDiscoveryShelfItem(current, nextRating);
  };

  const openRadioSearch = (query) => {
    setRadioOpen(false);
    navigate(buildPlayerRadioSearchPath(query));
  };

  const togglePlayback = useCallback(() => {
    if (!audioRef.current || !current) return;
    if (playing) {
      pause();
    } else {
      playAudio().catch(() => {});
    }
  }, [current, pause, playAudio, playing]);

  useEffect(() => {
    let cancelled = false;

    if (!current) {
      setSource('');
      return undefined;
    }

    if (current.streamUrl) {
      setSource(current.streamUrl);
      return undefined;
    }

    if (!current.contentId) {
      setSource('');
      return undefined;
    }

    streaming
      .createStreamTicket(current.contentId)
      .then((ticket) => {
        if (!cancelled) {
          setSource(ticket
            ? streaming.buildTicketedStreamUrl(current.contentId, ticket)
            : streaming.buildDirectStreamUrl(current.contentId));
        }
      })
      .catch(() => {
        if (!cancelled) setSource(streaming.buildDirectStreamUrl(current.contentId));
      });

    return () => {
      cancelled = true;
    };
  }, [current]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      const action = getPlayerShortcutAction(event);
      if (!action || !current) return;

      event.preventDefault();

      if (action === 'togglePlayback') {
        togglePlayback();
      } else if (action === 'seekBackward') {
        seekRelative(-15);
      } else if (action === 'seekForward') {
        seekRelative(30);
      } else if (action === 'previous') {
        previous();
      } else if (action === 'next') {
        next();
      } else if (action === 'toggleMute') {
        setLocalMuted((muted) => !muted);
      } else if (action === 'toggleEqualizer') {
        setEqPanelOpen((open) => !open);
      } else if (action === 'toggleLyrics') {
        setLyricsOpen((open) => !open);
      } else if (action === 'toggleVisualizer') {
        toggleVisualizer();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    current,
    next,
    previous,
    seekRelative,
    togglePlayback,
    toggleVisualizer,
  ]);

  useEffect(() => {
    if (!audioRef.current || !source) return;
    const previousSource = lastSourceRef.current;
    if (crossfadeEnabled && previousSource && previousSource !== source && fadeAudioRef.current) {
      fadeAudioRef.current.src = previousSource;
      fadeAudioRef.current.currentTime = audioRef.current.currentTime || 0;
      fadeAudioRef.current.play().then(() => {
        fadeOutputGain(fadeAudioRef.current, 1, 0, 5);
        window.setTimeout(() => fadeAudioRef.current?.pause(), 5200);
      }).catch(() => {});
      setOutputGain(audioRef.current, 0);
      fadeOutputGain(audioRef.current, 0, 1, 5);
    } else {
      setOutputGain(audioRef.current, 1);
    }
    lastSourceRef.current = source;
    audioRef.current.load();
    playAudio().catch(() => {});
  }, [crossfadeEnabled, playAudio, source]);

  useEffect(() => {
    if (!current?.artist || !current?.title) return;
    listenBrainz.submitListen('playing_now', current).catch(() => {});
    scrobbledRef.current = '';
  }, [current]);

  useEffect(() => {
    const audioElement = playerAudioElement;
    if (!audioElement || !current) return undefined;

    const handleTimeUpdate = () => {
      const duration = Number.isFinite(audioElement.duration)
        ? audioElement.duration
        : 0;
      const threshold = duration > 0
        ? Math.min(duration / 2, 240)
        : 240;
      const scrobbleKey = `${current.contentId}:${current.title}`;

        if (audioElement.currentTime >= threshold && scrobbledRef.current !== scrobbleKey) {
          scrobbledRef.current = scrobbleKey;
          recordLocalPlay(current);
          listenBrainz.submitListen('single', current).catch(() => {});
        }
    };

    audioElement.addEventListener('timeupdate', handleTimeUpdate);
    return () => audioElement.removeEventListener('timeupdate', handleTimeUpdate);
  }, [current, playerAudioElement]);

  const openPictureInPicture = async () => {
    if (!audioRef.current || !window.documentPictureInPicture) return;

    const graph = await resumeAudioGraph(audioRef.current);
    if (!graph) return;

    const pipWindow = await window.documentPictureInPicture.requestWindow({
      height: 220,
      width: 360,
    });
    pipWindow.document.body.style.margin = '0';
    pipWindow.document.body.style.background = '#050608';
    const canvas = pipWindow.document.createElement('canvas');
    canvas.style.height = '100%';
    canvas.style.width = '100%';
    pipWindow.document.body.appendChild(canvas);
    pipRef.current.win = pipWindow;

    const draw = () => {
      if (pipWindow.closed) return;
      const width = Math.max(1, pipWindow.innerWidth);
      const height = Math.max(1, pipWindow.innerHeight);
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext('2d');
      const data = new Uint8Array(graph.analyser.frequencyBinCount);
      graph.analyser.getByteFrequencyData(data);
      ctx.fillStyle = '#050608';
      ctx.fillRect(0, 0, width, height);
      const barCount = Math.min(72, Math.max(16, Math.floor(width / 7)));
      const bars = getFrequencyBars(data, barCount);
      const barWidth = width / bars.length;
      bars.forEach((value, index) => {
        const barHeight = (value / 255) * height;
        ctx.fillStyle = `hsl(${130 - (index / bars.length) * 100}, 75%, 54%)`;
        ctx.fillRect(
          index * barWidth,
          height - barHeight,
          Math.max(1, barWidth - 1),
          barHeight,
        );
      });
      pipRef.current.raf = pipWindow.requestAnimationFrame(draw);
    };

    draw();
  };

  useEffect(() => {
    if (!('mediaSession' in navigator) || !window.MediaMetadata) {
      return undefined;
    }
    if (!current) {
      navigator.mediaSession.metadata = null;
      return undefined;
    }

    navigator.mediaSession.metadata = new window.MediaMetadata({
      album: current.album || '',
      artist: current.artist || '',
      title: current.title || current.fileName || current.contentId,
    });

    const handlers = {
      nexttrack: next,
      pause,
      play: () => playAudio().catch(() => {}),
      previoustrack: previous,
      seekbackward: () => seekRelative(-15),
      seekforward: () => seekRelative(30),
    };

    Object.entries(handlers).forEach(([action, handler]) => {
      try {
        navigator.mediaSession.setActionHandler(action, handler);
      } catch {
        // Some browsers expose a partial Media Session implementation.
      }
    });

    return () => {
      Object.keys(handlers).forEach((action) => {
        try {
          navigator.mediaSession.setActionHandler(action, null);
        } catch {
          // Some browsers expose a partial Media Session implementation.
        }
      });
    };
  }, [current, next, pause, previous, seekRelative]);

  const audio = (
    <>
      <audio
        onLoadedMetadata={() => {
          if (audioRef.current && current?.positionSeconds > 0) {
            audioRef.current.currentTime = current.positionSeconds;
          }
        }}
        onEnded={next}
        onPause={() => setPlaying(false)}
        onPlay={() => setPlaying(true)}
        playsInline
        preload="metadata"
        ref={bindAudioElement}
        src={source || undefined}
      />
      <audio preload="metadata" ref={fadeAudioRef} />
    </>
  );
  const playerBadges = getPlayerBadges(current);

  if (collapsed) {
    return (
      <div
        className="player-bar player-bar-collapsed player-bar-modern"
        ref={playerBarRef}
      >
        {audio}
        <div className="player-track player-track-lcd">
          <Icon name="music" />
          <div>
            <div className="player-title">
              {current?.title || 'Player'}
            </div>
            <div className="player-subtitle">
              {current?.artist || 'Ready'}
            </div>
          </div>
        </div>
        <div className="player-controls player-control-cluster">
          <PlayerToolButton
            content="Expand the player drawer."
            aria-label="Expand player"
            data-testid="player-expand"
            icon="angle up"
            onClick={() => setCollapsed(false)}
          />
          <PlayerToolButton
            content={playing ? 'Pause the current stream.' : 'Resume the current stream.'}
            aria-label={playing ? 'Pause local playback' : 'Resume local playback'}
            data-testid="player-collapsed-toggle-playback"
            disabled={!current}
            icon={playing ? 'pause' : 'play'}
            onClick={togglePlayback}
          />
          <PlayerToolButton
            content={
              localMuted
                ? 'Unmute playback on this device without changing the stream.'
                : 'Mute playback on this device without changing the stream.'
            }
            aria-label={localMuted ? 'Unmute local playback' : 'Mute local playback'}
            data-testid="player-collapsed-toggle-mute"
            disabled={!current}
            icon={localMuted ? 'volume off' : 'volume up'}
            onClick={() => setLocalMuted((muted) => !muted)}
          />
        </div>
      </div>
    );
  }

  return (
    <div
      className="player-bar player-bar-modern"
      ref={playerBarRef}
    >
      {audio}
      <div className="player-main-deck">
        <div className="player-display">
          <PlayerVisualTile
            audioElement={playerAudioElement}
            current={current}
            mode={visualizerMode}
            onModeChange={setVisualizerMode}
            onTileModeChange={setVisualTileMode}
            tileMode={visualTileMode}
          />
          <div className="player-now-playing">
            <div className="player-track">
              <div>
                <div className="player-eyebrow">
                  {playing ? 'Now playing' : current ? 'Paused' : 'Ready'}
                </div>
                <div className="player-title">
                  {current?.title || 'Nothing playing'}
                </div>
                <div className="player-subtitle">
                  {current?.artist || 'Pick a collection or local audio file'}
                  {current?.album ? ` | ${current.album}` : ''}
                  {followingParty ? ` | Following ${followingParty.hostPeerId}` : ''}
                </div>
                {current ? (
                  <div className="player-now-playing-meta">
                    <div className="player-now-playing-badges">
                      {playerBadges.map((badge) => (
                        <Label
                          className="player-now-playing-badge"
                          color={badge.color}
                          data-testid={`player-badge-${badge.key}`}
                          key={badge.key}
                          size="mini"
                          title={badge.title}
                        >
                          <Icon name={badge.icon} />
                          {badge.text}
                        </Label>
                      ))}
                    </div>
                    <PlayerRatingControls
                      current={current}
                      onChange={updatePlayerRating}
                      rating={playerRating}
                    />
                  </div>
                ) : null}
              </div>
            </div>
            <div className="player-display-analyzers">
              <PlayerAnalyzerTile
                audioElement={current ? playerAudioElement : null}
                mode={analyzerMode}
                onModeChange={setAnalyzerMode}
              />
            </div>
          </div>
        </div>

        <div className="player-control-pad">
          <div className="player-control-row player-control-row-transport">
            <PlayerToolButton
              content="Go to the previous queue item, or restart the current stream."
              aria-label="Previous local track"
              data-testid="player-previous"
              disabled={!current}
              icon="step backward"
              onClick={previous}
            />
            <PlayerToolButton
              content="Rewind local playback by 15 seconds."
              aria-label="Rewind local playback"
              data-testid="player-rewind"
              disabled={!current}
              icon="backward"
              onClick={() => seekRelative(-15)}
            />
            <PlayerToolButton
              content={playing ? 'Pause the current stream.' : 'Resume the current stream.'}
              aria-label={playing ? 'Pause local playback' : 'Resume local playback'}
              className="player-play-button"
              data-testid="player-toggle-playback"
              disabled={!current}
              icon={playing ? 'pause' : 'play'}
              onClick={togglePlayback}
            />
            <PlayerToolButton
              content="Fast-forward local playback by 30 seconds."
              aria-label="Fast-forward local playback"
              data-testid="player-fast-forward"
              disabled={!current}
              icon="forward"
              onClick={() => seekRelative(30)}
            />
            <PlayerToolButton
              content="Play the next queue item."
              aria-label="Next local track"
              data-testid="player-next"
              disabled={!current || queue.length < 2}
              icon="step forward"
              onClick={next}
            />
            <PlayerToolButton
              content="Stop playback and clear your now-playing profile status."
              aria-label="Stop local playback"
              data-testid="player-stop"
              disabled={!current}
              icon="stop"
              onClick={clear}
            />
          </div>
          <div className="player-control-row">
            <PlayerLauncher
              compact
              onPlayItem={(item) => playItem(item, { replaceQueue: true })}
            />
            <PlayerToolButton
              active={queueOpen}
              content="Open the playback queue manager with current, upcoming, and recent session tracks."
              aria-label="Open playback queue"
              data-testid="player-open-queue"
              disabled={!current}
              icon="list ol"
              onClick={() => setQueueOpen(true)}
            />
            <PlayerToolButton
              active={localMuted}
              content={
                localMuted
                  ? 'Unmute playback on this device without changing the stream.'
                  : 'Mute playback on this device without changing the stream.'
              }
              aria-label={localMuted ? 'Unmute local playback' : 'Mute local playback'}
              data-testid="player-toggle-mute"
              disabled={!current}
              icon={localMuted ? 'volume off' : 'volume up'}
              onClick={() => setLocalMuted((muted) => !muted)}
            />
            <PlayerToolButton
              active={visualizerMode !== 'off'}
              content={
                visualizerMode === 'off'
                  ? 'Show the MilkDrop visualizer.'
                  : 'Hide the MilkDrop visualizer.'
              }
              aria-label={
                visualizerMode === 'off'
                  ? 'Show MilkDrop visualizer'
                  : 'Hide MilkDrop visualizer'
              }
              data-testid="player-toggle-visualizer"
              icon="eye"
              onClick={toggleVisualizer}
            />
            <PlayerToolButton
              active={eqPanelOpen}
              content={
                eqPanelOpen
                  ? 'Hide the equalizer panel.'
                  : 'Show the equalizer sliders and presets.'
              }
              aria-label={eqPanelOpen ? 'Hide equalizer' : 'Show equalizer'}
              data-testid="player-toggle-eq"
              icon="sliders horizontal"
              onClick={() => setEqPanelOpen((open) => !open)}
            />
            <PlayerToolButton
              active={lyricsOpen}
              content={
                lyricsOpen
                  ? 'Hide synced lyrics for the current track.'
                  : 'Fetch synced lyrics for the current artist and title from LRCLIB.'
              }
              aria-label={lyricsOpen ? 'Hide lyrics' : 'Show lyrics'}
              data-testid="player-toggle-lyrics"
              disabled={!current}
              icon="align left"
              onClick={() => setLyricsOpen((open) => !open)}
            />
            <PlayerToolButton
              content="Build smart-radio search seeds from the current track without starting network work yet."
              aria-label="Open smart radio seeds"
              data-testid="player-open-radio"
              disabled={!current}
              icon="random"
              onClick={() => setRadioOpen(true)}
            />
            <PlayerToolButton
              content="Show local listening stats recorded in this browser."
              aria-label="Open listening stats"
              data-testid="player-open-listening-stats"
              icon="bar chart"
              onClick={() => setStatsOpen(true)}
            />
            <PlayerToolButton
              active={shelfOpen}
              content="Open the browser-local discovery shelf built from player ratings."
              aria-label="Open discovery shelf"
              data-testid="player-open-discovery-shelf"
              icon="bookmark"
              onClick={() => setShelfOpen(true)}
            />
          </div>
          <div className="player-control-row">
            <PlayerToolButton
              content="Collapse the player into a small drawer bar above the footer."
              aria-label="Collapse player"
              data-testid="player-collapse"
              icon="angle down"
              onClick={() => setCollapsed(true)}
            />
            <PlayerToolButton
              active={karaokeEnabled}
              content={
                karaokeEnabled
                  ? 'Turn off center-channel vocal reduction.'
                  : 'Try center-channel vocal reduction for karaoke-style playback.'
              }
              aria-label={karaokeEnabled ? 'Disable karaoke mode' : 'Enable karaoke mode'}
              data-testid="player-toggle-karaoke"
              disabled={!current}
              icon="microphone slash"
              onClick={() => setKaraokeEnabledState((enabled) => !enabled)}
            />
            <PlayerToolButton
              active={crossfadeEnabled}
              content={
                crossfadeEnabled
                  ? 'Disable the five-second fade between queue items.'
                  : 'Enable a five-second fade between queue items.'
              }
              aria-label={crossfadeEnabled ? 'Disable crossfade' : 'Enable crossfade'}
              data-testid="player-toggle-crossfade"
              icon="exchange"
              onClick={() => setCrossfadeEnabled((enabled) => !enabled)}
            />
            <PlayerToolButton
              content="Open a tiny always-on-top spectrum window when this browser supports Document Picture-in-Picture."
              aria-label="Open visualizer picture in picture"
              data-testid="player-document-pip"
              disabled={!current || !window.documentPictureInPicture}
              icon="window restore"
              onClick={openPictureInPicture}
            />
            <PlayerToolButton
              active={listenBrainzToken.length > 0}
              content="Configure ListenBrainz scrobbling for this browser."
              aria-label="Configure ListenBrainz scrobbling"
              data-testid="player-open-integrations"
              icon="cloud upload"
              onClick={() => setIntegrationsOpen(true)}
            />
          </div>
        </div>
      </div>

      <div className="player-expanded-panels">
        {eqPanelOpen ? (
          <div className="player-panel player-panel-eq">
            <Equalizer audioElement={playerAudioElement} />
          </div>
        ) : null}
        <LyricsPane
          audioElement={playerAudioElement}
          current={current}
          visible={lyricsOpen}
        />
      </div>

      <Modal
        className="player-browser-modal player-integrations-modal"
        onClose={() => setIntegrationsOpen(false)}
        open={integrationsOpen}
        size="tiny"
      >
        <Modal.Header>Player Integrations</Modal.Header>
        <Modal.Content>
          <p className="player-modal-copy">
            ListenBrainz submissions are opt-in and stored only in this browser.
          </p>
          <Input
            aria-label="ListenBrainz user token"
            action={
              <Button
                aria-label="Clear ListenBrainz token"
                data-testid="player-clear-listenbrainz-token"
                icon
                onClick={() => {
                  setListenBrainzTokenState('');
                  listenBrainz.setListenBrainzToken('');
                }}
                type="button"
              >
                <Icon name="trash alternate outline" />
              </Button>
            }
            data-testid="player-listenbrainz-token"
            fluid
            icon="cloud upload"
            onChange={(event) => {
              setListenBrainzTokenState(event.target.value);
              listenBrainz.setListenBrainzToken(event.target.value);
            }}
            placeholder="ListenBrainz token"
            size="mini"
            type="password"
            value={listenBrainzToken}
          />
          <div
            className="player-token-save-state"
            data-testid="player-listenbrainz-save-state"
          >
            <Icon name="check circle outline" />
            Token changes are saved automatically in this browser.
          </div>
          <div
            className="player-external-visualizer"
            data-testid="player-external-visualizer"
          >
            <div className="player-panel-title">External Visualizer</div>
            <div className="player-external-visualizer-summary">
              <Icon
                name={externalVisualizerStatus?.enabled ? 'desktop' : 'ban'}
              />
              <div>
                <div className="player-external-visualizer-name">
                  {externalVisualizerStatus?.name || 'MilkDrop3'}
                </div>
                <div className="player-external-visualizer-status">
                  {getExternalVisualizerStatusText(
                    externalVisualizerStatus,
                    externalVisualizerLoading,
                  )}
                </div>
                {externalVisualizerStatus?.path ? (
                  <div
                    className="player-external-visualizer-path"
                    title={externalVisualizerStatus.path}
                  >
                    {externalVisualizerStatus.path}
                  </div>
                ) : null}
              </div>
            </div>
            {externalVisualizerMessage ? (
              <Message
                className="player-external-visualizer-message"
                compact
                data-testid="player-external-visualizer-message"
                info={externalVisualizerStatus?.available}
                size="tiny"
                warning={!externalVisualizerStatus?.available}
              >
                {externalVisualizerMessage}
              </Message>
            ) : null}
            <div className="player-external-visualizer-actions">
              <Popup
                content="Start the configured external visualizer on the slskdN host. Use this for MilkDrop3 or another local visualizer that captures system audio."
                trigger={
                  <Button
                    data-testid="player-launch-external-visualizer"
                    disabled={
                      externalVisualizerLaunching ||
                      !externalVisualizerStatus?.enabled ||
                      !externalVisualizerStatus?.available
                    }
                    loading={externalVisualizerLaunching}
                    onClick={launchExternalVisualizer}
                    size="mini"
                    type="button"
                  >
                    <Icon name="external alternate" />
                    Launch
                  </Button>
                }
              />
              <Popup
                content="Refresh the configured external visualizer path and readiness from the server."
                trigger={
                  <Button
                    data-testid="player-refresh-external-visualizer"
                    disabled={externalVisualizerLoading}
                    loading={externalVisualizerLoading}
                    onClick={refreshExternalVisualizerStatus}
                    size="mini"
                    type="button"
                  >
                    <Icon name="refresh" />
                    Refresh
                  </Button>
                }
              />
            </div>
          </div>
        </Modal.Content>
        <Modal.Actions>
          <Popup
            content="Close settings. ListenBrainz token changes have already been saved."
            trigger={
              <Button
                data-testid="player-close-integrations"
                onClick={() => setIntegrationsOpen(false)}
                primary
              >
                <Icon name="check" />
                Done
              </Button>
            }
          />
        </Modal.Actions>
      </Modal>
      <PlayerRadioModal
        current={current}
        onClose={() => setRadioOpen(false)}
        onOpenSearch={openRadioSearch}
        open={radioOpen}
      />
      <PlayerQueueModal
        current={current}
        history={history}
        onAutoQueueSimilar={queueItems}
        onClearQueue={clearQueue}
        onClose={() => setQueueOpen(false)}
        onNext={next}
        onPrevious={previous}
        onRemove={removeFromQueue}
        open={queueOpen}
        queue={queue}
      />
      <PlayerDiscoveryShelfModal
        onClose={() => setShelfOpen(false)}
        open={shelfOpen}
      />
      <PlayerStatsModal
        onClose={() => setStatsOpen(false)}
        onOpenSearch={(query) => {
          setStatsOpen(false);
          openRadioSearch(query);
        }}
        open={statsOpen}
      />
      {current && queue.length > 1 ? (
        <div className="player-queue">
          {queue.slice(1, 4).map((item) => (
            <button
              className="player-queue-item"
              key={item.contentId}
              onClick={() => removeFromQueue(item.contentId)}
              title="Remove this item from the visible queue."
              type="button"
            >
              {item.title || item.fileName || item.contentId}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
};

export default PlayerBar;
