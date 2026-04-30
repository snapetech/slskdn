import './Player.css';
import * as collectionsAPI from '../../lib/collections';
import * as externalVisualizer from '../../lib/externalVisualizer';
import * as listenBrainz from '../../lib/listenBrainz';
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
  Message,
  Modal,
  Popup,
  Segment,
  Table,
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
  const audioRef = useRef(null);
  const fadeAudioRef = useRef(null);
  const lastSourceRef = useRef('');
  const playerBarRef = useRef(null);
  const scrobbledRef = useRef('');
  const pipRef = useRef({ raf: null, win: null });
  const {
    clear,
    current,
    followingParty,
    next,
    pause,
    queue,
    previous,
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
  const [externalVisualizerStatus, setExternalVisualizerStatus] = useState(null);
  const [externalVisualizerLoading, setExternalVisualizerLoading] = useState(false);
  const [externalVisualizerLaunching, setExternalVisualizerLaunching] = useState(false);
  const [externalVisualizerMessage, setExternalVisualizerMessage] = useState('');
  const [playerAudioElement, setPlayerAudioElement] = useState(null);
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
            onClick={() => {
              if (!audioRef.current) return;
              if (playing) {
                pause();
              } else {
                playAudio().catch(() => {});
              }
            }}
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
              onClick={() => {
                if (!audioRef.current) return;
                if (playing) {
                  pause();
                } else {
                  playAudio().catch(() => {});
                }
              }}
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
