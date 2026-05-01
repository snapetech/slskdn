import './Wishlist.css';
import { urlBase } from '../../config';
import {
  buildWishlistRequestReviewPacket,
  buildWishlistRequestSummary,
  formatWishlistRequestReviewPacket,
  getWishlistRequestState,
  getRunnableWishlistRequests,
} from '../../lib/acquisitionRequests';
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
  item,
  onDelete,
  onEdit,
  onRunSearch,
}) => {
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [running, setRunning] = useState(false);
  const requestState = getWishlistRequestState(item, []);

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

const Wishlist = () => {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalItem, setModalItem] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [showImportModal, setShowImportModal] = useState(false);
  const [requestCopyStatus, setRequestCopyStatus] = useState('');
  const [bulkRunning, setBulkRunning] = useState(false);
  const requestSummary = useMemo(
    () =>
      buildWishlistRequestSummary({
        items,
      }),
    [items],
  );
  const runnableRequests = useMemo(
    () => getRunnableWishlistRequests(items, { limit: 3 }),
    [items],
  );

  const copyRequestReviewPacket = async () => {
    const packet = buildWishlistRequestReviewPacket({
      items,
    });
    const report = formatWishlistRequestReviewPacket(packet);

    if (!navigator.clipboard?.writeText) {
      setRequestCopyStatus('Clipboard unavailable; copy the request summary manually.');
      return;
    }

    try {
      await navigator.clipboard.writeText(report);
      setRequestCopyStatus('Wishlist request review copied.');
    } catch {
      setRequestCopyStatus('Unable to copy Wishlist request review.');
    }
  };

  const runEnabledSearches = async () => {
    setBulkRunning(true);
    const results = [];

    try {
      for (const item of runnableRequests) {
        try {
          const result = await wishlistAPI.runSearch(item.id);
          results.push({
            id: item.id,
            responseCount: result.responseCount ?? result.ResponseCount ?? 0,
            status: 'ran',
          });
        } catch (error) {
          results.push({
            error: error.message || 'Search failed',
            id: item.id,
            status: 'failed',
          });
        }
      }

      const ran = results.filter((result) => result.status === 'ran').length;
      const failed = results.filter((result) => result.status === 'failed').length;
      setRequestCopyStatus(
        `Ran ${ran} enabled Wishlist search${ran === 1 ? '' : 'es'}${
          failed ? `; ${failed} failed` : ''
        }. Downloads still require normal result selection and policy.`,
      );
      await loadItems();
    } finally {
      setBulkRunning(false);
    }
  };

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
      </Segment>

      {!loading && (
        <Segment className="wishlist-request-summary">
          <div className="wishlist-request-summary-header">
            <Header as="h3">
              <Icon name="clipboard check" />
              Request Portal Summary
              <Header.Subheader>
                Operator view of wanted music before acquisition jobs are wired.
              </Header.Subheader>
            </Header>
            <Popup
              content="Copy the current Wishlist request review packet. This does not start searches, peer browsing, downloads, or automation."
              position="top center"
              trigger={
                <Button
                  aria-label="Copy Wishlist request review"
                  onClick={copyRequestReviewPacket}
                  size="small"
                >
                  <Icon name="copy" />
                  Copy Review
                </Button>
              }
            />
            <Popup
              content="Run up to three enabled Wishlist searches now through the backend. This starts search jobs only; downloads still require the normal result selection and policy."
              position="top center"
              trigger={
                <Button
                  aria-label="Run enabled Wishlist searches"
                  disabled={runnableRequests.length === 0}
                  loading={bulkRunning}
                  onClick={runEnabledSearches}
                  primary
                  size="small"
                >
                  <Icon name="play" />
                  Run Enabled
                </Button>
              }
            />
          </div>
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
          {requestCopyStatus && (
            <Label
              basic
              color="purple"
            >
              {requestCopyStatus}
            </Label>
          )}
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
                item={item}
                key={item.id}
                onDelete={handleDelete}
                onEdit={handleEdit}
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
    </div>
  );
};

export default Wishlist;
