import './Wishlist.css';
import { urlBase } from '../../config';
import * as wishlistAPI from '../../lib/wishlist';
import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Confirm,
  Form,
  Header,
  Icon,
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

const WishlistItemRow = ({ item, onDelete, onEdit, onRunSearch }) => {
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [running, setRunning] = useState(false);

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

const Wishlist = () => {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalItem, setModalItem] = useState(null);
  const [showModal, setShowModal] = useState(false);

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
      </Segment>

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
    </div>
  );
};

export default Wishlist;
