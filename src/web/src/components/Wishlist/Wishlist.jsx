import './Wishlist.css';
import { urlBase } from '../../config';
import {
  addWishlistItemToDiscoveryInbox,
  buildWishlistRequestSummary,
  getWishlistRequestState,
} from '../../lib/acquisitionRequests';
import {
  addDiscoveryInboxItem,
  getDiscoveryInboxItems,
} from '../../lib/discoveryInbox';
import * as sourceFeedImportsAPI from '../../lib/sourceFeedImports';
import * as spotifyIntegrationAPI from '../../lib/spotifyIntegration';
import * as wishlistAPI from '../../lib/wishlist';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Confirm,
  Form,
  Header,
  Icon,
  Label,
  Modal,
  Popup,
  Segment,
  Table,
} from 'semantic-ui-react';

const formatDate = (dateString) => {
  if (!dateString) return 'Never';
  const date = new Date(dateString);
  return date.toLocaleString();
};

const WishlistItemRow = ({
  inboxItems,
  item,
  onDelete,
  onEdit,
  onReview,
  onRunSearch,
}) => {
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [running, setRunning] = useState(false);
  const requestState = getWishlistRequestState(item, inboxItems);

  const handleRunSearch = async () => {
    setRunning(true);
    try {
      const result = await onRunSearch(item.id);
      toast.success(`Search completed with ${result.responseCount} results`);
    } catch (error) {
      toast.error(`Search failed: ${error.message}`);
    } finally {
      setRunning(false);
    }
  };

  return (
    <Table.Row>
      <Table.Cell>
        <Icon
          color={item.enabled ? 'green' : 'grey'}
          name={item.enabled ? 'check circle' : 'circle outline'}
        />
      </Table.Cell>
      <Table.Cell>
        <strong>{item.searchText}</strong>
        {item.filter && (
          <div className="wishlist-filter">Filter: {item.filter}</div>
        )}
      </Table.Cell>
      <Table.Cell textAlign="center">
        <Popup
          content="Auto-download best matches"
          trigger={
            <Icon
              color={item.autoDownload ? 'green' : 'grey'}
              name={item.autoDownload ? 'download' : 'download'}
            />
          }
        />
      </Table.Cell>
      <Table.Cell>{formatDate(item.lastSearchedAt)}</Table.Cell>
      <Table.Cell textAlign="center">{item.lastMatchCount}</Table.Cell>
      <Table.Cell textAlign="center">{item.totalSearchCount}</Table.Cell>
      <Table.Cell>
        <Popup
          content={requestState.summary}
          position="top center"
          trigger={
            <Label color={requestState.color}>
              {requestState.label}
            </Label>
          }
        />
      </Table.Cell>
      <Table.Cell>
        {item.lastSearchId && (
          <Link to={`${urlBase}/searches/${item.lastSearchId}`}>
            <Button
              compact
              icon="search"
              size="tiny"
              title="View last search results"
            />
          </Link>
        )}
        <Button
          compact
          icon="play"
          loading={running}
          onClick={handleRunSearch}
          primary
          size="tiny"
          title="Run search now"
        />
        <Popup
          content="Send this wishlist request to the Discovery Inbox for approval, rejection, or snoozing before any acquisition job is started."
          position="top center"
          trigger={
            <Button
              aria-label={`Send ${item.searchText} to Discovery Inbox review`}
              compact
              icon="inbox"
              onClick={() => onReview(item)}
              size="tiny"
              title="Send to Discovery Inbox review"
            />
          }
        />
        <Button
          compact
          icon="edit"
          onClick={() => onEdit(item)}
          size="tiny"
          title="Edit"
        />
        <Button
          color="red"
          compact
          icon="trash"
          onClick={() => setConfirmDelete(true)}
          size="tiny"
          title="Delete"
        />
        <Confirm
          cancelButton="Cancel"
          confirmButton="Delete"
          content={`Delete wishlist item "${item.searchText}"?`}
          header="Confirm Delete"
          onCancel={() => setConfirmDelete(false)}
          onConfirm={() => {
            setConfirmDelete(false);
            onDelete(item.id);
          }}
          open={confirmDelete}
          size="mini"
        />
      </Table.Cell>
    </Table.Row>
  );
};

const WishlistModal = ({ item, onClose, onSave }) => {
  const [searchText, setSearchText] = useState(item?.searchText || '');
  const [filter, setFilter] = useState(item?.filter || '');
  const [enabled, setEnabled] = useState(item?.enabled ?? true);
  const [autoDownload, setAutoDownload] = useState(item?.autoDownload ?? false);
  const [maxResults, setMaxResults] = useState(item?.maxResults ?? 100);
  const [saving, setSaving] = useState(false);

  const isEdit = Boolean(item?.id);

  const handleSave = async () => {
    if (!searchText.trim()) {
      toast.error('Search text is required');
      return;
    }

    setSaving(true);
    try {
      await onSave({
        autoDownload,
        enabled,
        filter: filter.trim() || undefined,
        id: item?.id,
        maxResults,
        searchText: searchText.trim(),
      });
      onClose();
    } catch (error) {
      toast.error(`Failed to save: ${error.message}`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      onClose={onClose}
      open
      size="small"
    >
      <Modal.Header>
        <Icon name="star" />
        {isEdit ? 'Edit Wishlist Item' : 'Add to Wishlist'}
      </Modal.Header>
      <Modal.Content>
        <Form>
          <Form.Input
            label="Search Text"
            onChange={(event) => setSearchText(event.target.value)}
            placeholder="Enter search terms..."
            required
            value={searchText}
          />
          <Form.Input
            label="Filter (optional)"
            onChange={(event) => setFilter(event.target.value)}
            placeholder="e.g., flac OR mp3"
            value={filter}
          />
          <Form.Input
            label="Max Results"
            max={1_000}
            min={10}
            onChange={(event) =>
              setMaxResults(Number.parseInt(event.target.value, 10) || 100)
            }
            type="number"
            value={maxResults}
          />
          <Form.Field>
            <Checkbox
              checked={enabled}
              label="Enabled (run automatically)"
              onChange={(_, data) => setEnabled(data.checked)}
              toggle
            />
          </Form.Field>
          <Form.Field>
            <Checkbox
              checked={autoDownload}
              label="Auto-download best matches"
              onChange={(_, data) => setAutoDownload(data.checked)}
              toggle
            />
          </Form.Field>
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          loading={saving}
          onClick={handleSave}
          primary
        >
          {isEdit ? 'Save' : 'Add'}
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

const CsvImportModal = ({ onClose, onImport }) => {
  const [csvText, setCsvText] = useState('');
  const [filter, setFilter] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [autoDownload, setAutoDownload] = useState(false);
  const [includeAlbum, setIncludeAlbum] = useState(false);
  const [maxResults, setMaxResults] = useState(100);
  const [importing, setImporting] = useState(false);

  const handleFile = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setCsvText(await file.text());
  };

  const handleImport = async () => {
    if (!csvText.trim()) {
      toast.error('CSV text is required');
      return;
    }

    setImporting(true);
    try {
      await onImport({
        autoDownload,
        csvText,
        enabled,
        filter: filter.trim() || undefined,
        includeAlbum,
        maxResults,
      });
      onClose();
    } catch (error) {
      toast.error(`CSV import failed: ${error.message}`);
    } finally {
      setImporting(false);
    }
  };

  return (
    <Modal
      onClose={onClose}
      open
      size="small"
    >
      <Modal.Header>
        <Icon name="file alternate outline" />
        Import CSV Playlist
      </Modal.Header>
      <Modal.Content>
        <Form>
          <Form.Input
            accept=".csv,text/csv"
            label="CSV File"
            onChange={handleFile}
            type="file"
          />
          <Form.TextArea
            label="CSV Text"
            onChange={(event) => setCsvText(event.target.value)}
            placeholder="Track name,Artist name,Album name"
            rows={8}
            value={csvText}
          />
          <Form.Input
            label="Filter (optional)"
            onChange={(event) => setFilter(event.target.value)}
            placeholder="e.g., flac OR mp3"
            value={filter}
          />
          <Form.Input
            label="Max Results"
            max={1_000}
            min={1}
            onChange={(event) =>
              setMaxResults(Number.parseInt(event.target.value, 10) || 100)
            }
            type="number"
            value={maxResults}
          />
          <Form.Group widths="equal">
            <Form.Field>
              <Checkbox
                checked={enabled}
                label="Enabled"
                onChange={(_, data) => setEnabled(data.checked)}
                toggle
              />
            </Form.Field>
            <Form.Field>
              <Checkbox
                checked={autoDownload}
                label="Auto-download matches"
                onChange={(_, data) => setAutoDownload(data.checked)}
                toggle
              />
            </Form.Field>
            <Form.Field>
              <Checkbox
                checked={includeAlbum}
                label="Include album"
                onChange={(_, data) => setIncludeAlbum(data.checked)}
                toggle
              />
            </Form.Field>
          </Form.Group>
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Close the CSV importer without adding any wishlist searches."
          trigger={<Button onClick={onClose}>Cancel</Button>}
        />
        <Popup
          content="Create wishlist searches from the parsed CSV rows using the selected options."
          trigger={
            <Button
              loading={importing}
              onClick={handleImport}
              primary
            >
              Import
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const sourceKindOptions = [
  { key: 'auto', text: 'Auto detect', value: 'auto' },
  { key: 'spotify', text: 'Spotify URL/token feed', value: 'spotify' },
  { key: 'csv', text: 'CSV export', value: 'csv' },
  { key: 'text', text: 'Pasted tracklist', value: 'text' },
  { key: 'm3u', text: 'M3U/PLS playlist', value: 'm3u' },
  { key: 'rss', text: 'RSS/OPML feed', value: 'rss' },
];

const buildInboxItemFromSourceSuggestion = (suggestion) => ({
  evidenceKey: suggestion.evidenceKey,
  networkImpact:
    'Review only; importing this source feed item does not start Soulseek search, peer browse, or download work.',
  reason: suggestion.reason,
  searchText: suggestion.searchText,
  source: suggestion.source || 'Source Feed',
  sourceId: suggestion.sourceId || suggestion.providerUrl || '',
  title: suggestion.searchText || suggestion.title,
});

const SourceFeedImportModal = ({ onClose, onImported }) => {
  const [sourceText, setSourceText] = useState('');
  const [sourceKind, setSourceKind] = useState('auto');
  const [providerAccessToken, setProviderAccessToken] = useState('');
  const [includeAlbum, setIncludeAlbum] = useState(false);
  const [fetchProviderUrls, setFetchProviderUrls] = useState(true);
  const [limit, setLimit] = useState(500);
  const [preview, setPreview] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  const [spotifyStatus, setSpotifyStatus] = useState(null);
  const [spotifyConnecting, setSpotifyConnecting] = useState(false);

  const loadSpotifyStatus = useCallback(async () => {
    try {
      setSpotifyStatus(await spotifyIntegrationAPI.getSpotifyStatus());
    } catch {
      setSpotifyStatus(null);
    }
  }, []);

  useEffect(() => {
    loadSpotifyStatus();
  }, [loadSpotifyStatus]);

  useEffect(() => {
    const handleMessage = (event) => {
      if (
        event.origin === window.location.origin &&
        event.data?.type === 'slskdn:spotify-connected'
      ) {
        setSpotifyConnecting(false);
        loadSpotifyStatus();
      }
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [loadSpotifyStatus]);

  const handleFile = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setSourceText(await file.text());
  };

  const handlePreview = async () => {
    if (!sourceText.trim()) {
      toast.error('Source text or URL is required');
      return;
    }

    setPreviewing(true);
    try {
      const result = await sourceFeedImportsAPI.previewSourceFeedImport({
        fetchProviderUrls,
        includeAlbum,
        limit,
        providerAccessToken,
        sourceKind,
        sourceText,
      });
      setPreview(result);
      if (result.requiresAccessToken) {
        toast.warn(`Provider token required: ${result.requiredScopeHint}`);
      } else {
        toast.success(`Previewed ${result.suggestionCount} source items`);
      }
    } catch (error) {
      toast.error(`Source import preview failed: ${error.message}`);
    } finally {
      setPreviewing(false);
    }
  };

  const handleConnectSpotify = async () => {
    setSpotifyConnecting(true);
    try {
      const authorization = await spotifyIntegrationAPI.startSpotifyAuthorization();
      const popup = window.open(
        authorization.authorizationUrl,
        'slskdn-spotify-connect',
        'popup=yes,width=520,height=720',
      );
      if (!popup) {
        window.location.href = authorization.authorizationUrl;
        return;
      }

      const interval = window.setInterval(() => {
        if (popup.closed) {
          window.clearInterval(interval);
          loadSpotifyStatus();
          setSpotifyConnecting(false);
        }
      }, 750);
    } catch (error) {
      setSpotifyConnecting(false);
      toast.error(`Spotify connection failed: ${error.message}`);
    }
  };

  const handleDisconnectSpotify = async () => {
    try {
      await spotifyIntegrationAPI.disconnectSpotify();
      await loadSpotifyStatus();
      toast.success('Disconnected Spotify account');
    } catch (error) {
      toast.error(`Spotify disconnect failed: ${error.message}`);
    }
  };

  const handleAddToInbox = () => {
    if (!preview?.suggestions?.length) {
      toast.error('Preview has no suggestions to import');
      return;
    }

    let added = 0;
    preview.suggestions.forEach((suggestion) => {
      const item = addDiscoveryInboxItem(
        buildInboxItemFromSourceSuggestion(suggestion),
      );
      if (item.evidenceKey === suggestion.evidenceKey) {
        added += 1;
      }
    });
    onImported();
    toast.success(`Added ${added} source suggestions to Discovery Inbox`);
    onClose();
  };

  return (
    <Modal
      onClose={onClose}
      open
      size="large"
    >
      <Modal.Header>
        <Icon name="rss" />
        Import Source Feed
      </Modal.Header>
      <Modal.Content scrolling>
        <Form>
          <Form.Select
            label="Source Type"
            onChange={(_, data) => setSourceKind(data.value)}
            options={sourceKindOptions}
            value={sourceKind}
          />
          <Form.Input
            accept=".csv,.m3u,.m3u8,.pls,.opml,.xml,.rss,text/*"
            label="Source File"
            onChange={handleFile}
            type="file"
          />
          <Form.TextArea
            label="Source URL or Text"
            onChange={(event) => setSourceText(event.target.value)}
            placeholder="Paste a Spotify playlist/album/track/artist URL, spotify:liked, a CSV export, an M3U playlist, RSS/OPML, or one track per line."
            rows={7}
            value={sourceText}
          />
          <Segment>
            <Header as="h4">
              <Icon name="spotify" />
              Spotify Account
            </Header>
            <div className="wishlist-source-feed-account-row">
              <Label color={spotifyStatus?.connected ? 'green' : 'grey'}>
                <Icon name={spotifyStatus?.connected ? 'check circle' : 'minus circle'} />
                {spotifyStatus?.connected
                  ? `Connected${spotifyStatus.displayName ? `: ${spotifyStatus.displayName}` : ''}`
                  : spotifyStatus?.configured
                    ? 'Ready to connect'
                    : 'Not configured'}
              </Label>
              <Popup
                content="Open Spotify authorization so liked songs, saved albums, followed artists, and private playlists can be imported without pasting a temporary bearer token."
                trigger={
                  <Button
                    disabled={!spotifyStatus?.configured}
                    icon
                    labelPosition="left"
                    loading={spotifyConnecting}
                    onClick={handleConnectSpotify}
                    size="small"
                  >
                    <Icon name="plug" />
                    Connect
                  </Button>
                }
              />
              <Popup
                content="Remove the stored Spotify refresh token from this slskdN instance."
                trigger={
                  <Button
                    disabled={!spotifyStatus?.connected}
                    icon
                    labelPosition="left"
                    onClick={handleDisconnectSpotify}
                    size="small"
                  >
                    <Icon name="unlinkify" />
                    Disconnect
                  </Button>
                }
              />
            </div>
          </Segment>
          <Form.Input
            label="Provider Bearer Token (optional override)"
            onChange={(event) => setProviderAccessToken(event.target.value)}
            placeholder="Only needed when you do not want to use the connected Spotify account"
            type="password"
            value={providerAccessToken}
          />
          <Form.Group widths="equal">
            <Form.Input
              label="Limit"
              max={5_000}
              min={1}
              onChange={(event) =>
                setLimit(Number.parseInt(event.target.value, 10) || 500)
              }
              type="number"
              value={limit}
            />
            <Form.Field>
              <Checkbox
                checked={includeAlbum}
                label="Include album in searches"
                onChange={(_, data) => setIncludeAlbum(data.checked)}
                toggle
              />
            </Form.Field>
            <Form.Field>
              <Checkbox
                checked={fetchProviderUrls}
                label="Fetch provider URLs"
                onChange={(_, data) => setFetchProviderUrls(data.checked)}
                toggle
              />
            </Form.Field>
          </Form.Group>
        </Form>

        {preview && (
          <Segment>
            <Header as="h4">
              <Icon name="list" />
              <Header.Content>
                Preview
                <Header.Subheader>
                  {preview.suggestionCount} suggestions,{' '}
                  {preview.duplicateCount} duplicates, {preview.skippedCount}{' '}
                  skipped, {preview.networkRequestCount} provider requests
                </Header.Subheader>
              </Header.Content>
            </Header>
            {preview.requiresAccessToken && (
              <Label color="yellow">
                Token required: {preview.requiredScopeHint}
              </Label>
            )}
            {preview.suggestions?.length > 0 && (
              <Table
                celled
                compact
              >
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell>Search</Table.HeaderCell>
                    <Table.HeaderCell>Source</Table.HeaderCell>
                    <Table.HeaderCell>Album</Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {preview.suggestions.slice(0, 25).map((suggestion) => (
                    <Table.Row key={suggestion.evidenceKey}>
                      <Table.Cell>{suggestion.searchText}</Table.Cell>
                      <Table.Cell>{suggestion.source}</Table.Cell>
                      <Table.Cell>{suggestion.album || '-'}</Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Segment>
        )}
      </Modal.Content>
      <Modal.Actions>
        <Popup
          content="Close the source importer without adding anything to Discovery Inbox."
          trigger={<Button onClick={onClose}>Cancel</Button>}
        />
        <Popup
          content="Fetch and parse the source feed into a local preview. This does not start Soulseek searches or downloads."
          trigger={
            <Button
              loading={previewing}
              onClick={handlePreview}
              primary
            >
              Preview
            </Button>
          }
        />
        <Popup
          content="Add the previewed items to Discovery Inbox for approval before any acquisition work can start."
          trigger={
            <Button
              disabled={!preview?.suggestions?.length}
              icon
              labelPosition="left"
              onClick={handleAddToInbox}
            >
              <Icon name="inbox" />
              Add to Inbox
            </Button>
          }
        />
      </Modal.Actions>
    </Modal>
  );
};

const Wishlist = () => {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalItem, setModalItem] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [showImportModal, setShowImportModal] = useState(false);
  const [showSourceImportModal, setShowSourceImportModal] = useState(false);
  const [inboxItems, setInboxItems] = useState(() => getDiscoveryInboxItems());
  const requestSummary = useMemo(
    () =>
      buildWishlistRequestSummary({
        inboxItems,
        items,
      }),
    [inboxItems, items],
  );

  const loadItems = useCallback(async () => {
    try {
      const data = await wishlistAPI.getAll();
      setItems(data);
    } catch (error) {
      toast.error(`Failed to load wishlist: ${error.message}`);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadItems();
  }, [loadItems]);

  const handleAdd = () => {
    setModalItem(null);
    setShowModal(true);
  };

  const handleImportClick = () => {
    setShowImportModal(true);
  };

  const handleSourceImportClick = () => {
    setShowSourceImportModal(true);
  };

  const handleEdit = (item) => {
    setModalItem(item);
    setShowModal(true);
  };

  const handleSave = async (item) => {
    if (item.id) {
      await wishlistAPI.update(item.id, item);
      toast.success('Wishlist item updated');
    } else {
      await wishlistAPI.create(item);
      toast.success('Added to wishlist');
    }

    await loadItems();
  };

  const handleDelete = async (id) => {
    try {
      await wishlistAPI.remove(id);
      toast.success('Wishlist item deleted');
      await loadItems();
    } catch (error) {
      toast.error(`Failed to delete: ${error.message}`);
    }
  };

  const handleRunSearch = async (id) => {
    const result = await wishlistAPI.runSearch(id);
    await loadItems();
    return result;
  };

  const handleReview = (item) => {
    const inboxItem = addWishlistItemToDiscoveryInbox(item);
    setInboxItems(getDiscoveryInboxItems());
    toast.success(`Added "${inboxItem.title}" to Discovery Inbox`);
  };

  const handleImport = async (request) => {
    const result = await wishlistAPI.importCsv(request);
    toast.success(
      `Imported ${result.createdCount} searches (${result.duplicateCount} duplicates, ${result.skippedCount} skipped)`,
    );
    await loadItems();
  };

  return (
    <div className="wishlist-container">
      <Segment
        className="wishlist-header"
        clearing
      >
        <Header
          as="h2"
          floated="left"
        >
          <Icon name="star" />
          <Header.Content>
            Wishlist
            <Header.Subheader>
              Saved searches that run automatically
            </Header.Subheader>
          </Header.Content>
        </Header>
        <Popup
          content="Add one saved search to the wishlist. Enabled wishlist entries run later using the normal conservative scheduler."
          trigger={
            <Button
              floated="right"
              icon
              labelPosition="left"
              onClick={handleAdd}
              primary
            >
              <Icon name="plus" />
              Add Search
            </Button>
          }
        />
        <Popup
          content="Import a playlist CSV, such as a TuneMyMusic export, into wishlist searches without starting a large search burst immediately."
          trigger={
            <Button
              floated="right"
              icon
              labelPosition="left"
              onClick={handleImportClick}
            >
              <Icon name="file alternate outline" />
              Import CSV
            </Button>
          }
        />
        <Popup
          content="Preview Spotify URLs, liked/saved/followed feeds with a provider token, CSV/text playlists, M3U, and RSS/OPML sources into Discovery Inbox review without starting searches or downloads."
          trigger={
            <Button
              floated="right"
              icon
              labelPosition="left"
              onClick={handleSourceImportClick}
            >
              <Icon name="rss" />
              Import Feed
            </Button>
          }
        />
      </Segment>

      {!loading && (
        <Segment className="wishlist-request-summary">
          <Header as="h3">
            <Icon name="clipboard check" />
            Request Portal Summary
            <Header.Subheader>
              Operator view of wanted music before acquisition jobs are wired.
            </Header.Subheader>
          </Header>
          <div className="wishlist-request-summary-grid">
            <Label color="purple">
              Requests
              <Label.Detail>{requestSummary.total}</Label.Detail>
            </Label>
            <Label color="green">
              Enabled
              <Label.Detail>{requestSummary.enabled}</Label.Detail>
            </Label>
            <Label color="blue">
              Automatic
              <Label.Detail>{requestSummary.automatic}</Label.Detail>
            </Label>
            <Label color={requestSummary.reviewCount > 0 ? 'yellow' : 'grey'}>
              Needs Review
              <Label.Detail>{requestSummary.reviewCount}</Label.Detail>
            </Label>
            <Label color={requestSummary.quotaStatus === 'Within quota' ? 'green' : 'orange'}>
              {requestSummary.quotaStatus}
              <Label.Detail>{requestSummary.quotaRemaining} left</Label.Detail>
            </Label>
          </div>
        </Segment>
      )}

      {loading ? (
        <Segment
          loading
          placeholder
        />
      ) : items.length === 0 ? (
        <Segment
          inverted
          placeholder
        >
          <Header
            icon
            inverted
          >
            <Icon name="star outline" />
            No wishlist items yet
          </Header>
          <p>
            Add searches to your wishlist and they&apos;ll run automatically.
          </p>
          <Button
            onClick={handleAdd}
            primary
          >
            Add Your First Search
          </Button>
        </Segment>
      ) : (
        <Table
          celled
          striped
        >
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell width={1}>Active</Table.HeaderCell>
              <Table.HeaderCell>Search</Table.HeaderCell>
              <Table.HeaderCell
                textAlign="center"
                width={1}
              >
                Auto
              </Table.HeaderCell>
              <Table.HeaderCell width={3}>Last Run</Table.HeaderCell>
              <Table.HeaderCell
                textAlign="center"
                width={1}
              >
                Matches
              </Table.HeaderCell>
              <Table.HeaderCell
                textAlign="center"
                width={1}
              >
                Runs
              </Table.HeaderCell>
              <Table.HeaderCell width={2}>Request State</Table.HeaderCell>
              <Table.HeaderCell width={3}>Actions</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {items.map((item) => (
              <WishlistItemRow
                inboxItems={inboxItems}
                item={item}
                key={item.id}
                onDelete={handleDelete}
                onEdit={handleEdit}
                onReview={handleReview}
                onRunSearch={handleRunSearch}
              />
            ))}
          </Table.Body>
        </Table>
      )}

      {showModal && (
        <WishlistModal
          item={modalItem}
          onClose={() => setShowModal(false)}
          onSave={handleSave}
        />
      )}

      {showImportModal && (
        <CsvImportModal
          onClose={() => setShowImportModal(false)}
          onImport={handleImport}
        />
      )}
      {showSourceImportModal && (
        <SourceFeedImportModal
          onClose={() => setShowSourceImportModal(false)}
          onImported={() => setInboxItems(getDiscoveryInboxItems())}
        />
      )}
    </div>
  );
};

export default Wishlist;
