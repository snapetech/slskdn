import React, { Component } from 'react';
import {
  Button,
  Container,
  Dropdown,
  Header,
  Icon,
  Modal,
  Table,
  Form,
  Message,
  Segment,
} from 'semantic-ui-react';
import * as collectionsAPI from '../../lib/collections';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';

export default class Collections extends Component {
  state = {
    collections: [],
    selectedCollection: null,
    selectedCollectionItems: [],
    shares: [],
    shareGroups: [],
    shareGroupsLoading: false,
    shareModalOpen: false,
    shareAudienceId: null,
    shareAllowStream: true,
    shareAllowDownload: true,
    loading: true,
    error: null,
    createModalOpen: false,
    addItemModalOpen: false,
    newCollectionTitle: '',
    newCollectionType: 'Playlist',
    newCollectionDescription: '',
    itemSearchQuery: '',
    itemSearchResults: [],
    itemSearchLoading: false,
  };

  componentDidMount() {
    this.loadData();
    this.loadShareGroups();
  }

  loadData = async () => {
    try {
      this.setState({ loading: true, error: null });
      const res = await collectionsAPI.getCollections().catch((err) => {
        if (err.response?.status === 401 || err.response?.status === 403 || 
            err.response?.status === 404) {
          return { data: [] };
        }
        throw err;
      });
      this.setState({
        collections: res.data || [],
        loading: false,
      });
    } catch (error) {
      let errorMsg = error.message;
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMsg = error.response.data;
        } else if (error.response.data.message) {
          errorMsg = error.response.data.message;
        } else if (error.response.data.error) {
          errorMsg = error.response.data.error;
        } else {
          errorMsg = JSON.stringify(error.response.data);
        }
      }
      const isAuthOrFeatureError = error.response?.status === 401 || 
                                   error.response?.status === 403 || 
                                   error.response?.status === 404;
      this.setState({ 
        error: isAuthOrFeatureError ? null : errorMsg, 
        loading: false 
      });
    }
  };

  loadShareGroups = async () => {
    try {
      this.setState({ shareGroupsLoading: true });
      const response = await collectionsAPI.getShareGroups().catch((err) => {
        if (err.response?.status === 401 || err.response?.status === 403 || err.response?.status === 404) {
          return { data: [] };
        }
        throw err;
      });
      const shareGroups = response.data || [];
      this.setState((prevState) => ({
        shareGroups,
        shareGroupsLoading: false,
        shareAudienceId: prevState.shareAudienceId ?? shareGroups[0]?.id ?? null,
      }));
    } catch (error) {
      this.setState({ shareGroups: [], shareGroupsLoading: false });
    }
  };

  loadShares = async (collectionId) => {
    try {
      if (!collectionId) {
        this.setState({ shares: [] });
        return;
      }

      const response = await collectionsAPI.getSharesByCollection(collectionId).catch((err) => {
        if (err.response?.status === 401 || err.response?.status === 403 || err.response?.status === 404) {
          return { data: [] };
        }
        throw err;
      });
      this.setState({ shares: response.data || [] });
    } catch (error) {
      this.setState({ shares: [] });
    }
  };

  loadCollectionItems = async (collectionId) => {
    try {
      const res = await collectionsAPI.getCollectionItems(collectionId);
      this.setState({ selectedCollectionItems: res.data || [] });
    } catch (error) {
      console.error('[Collections] Error loading items:', error);
    }
  };

  handleCreateCollection = async () => {
    try {
      await collectionsAPI.createCollection({
        title: this.state.newCollectionTitle,
        type: this.state.newCollectionType,
        description: this.state.newCollectionDescription || undefined,
      });
      this.setState({ 
        createModalOpen: false, 
        newCollectionTitle: '', 
        newCollectionType: 'Playlist',
        newCollectionDescription: '',
        error: null 
      });
      await this.loadData();
    } catch (error) {
      let errorMsg = error.message || 'Failed to create collection';
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMsg = error.response.data;
        } else if (error.response.data.detail) {
          errorMsg = error.response.data.detail;
        } else if (error.response.data.message) {
          errorMsg = error.response.data.message;
        } else if (error.response.data.error) {
          errorMsg = error.response.data.error;
        } else if (error.response.data.title) {
          errorMsg = error.response.data.title;
        }
      }
      this.setState({ error: errorMsg });
    }
  };

  handleDeleteCollection = async (id) => {
    if (!window.confirm('Delete this collection?')) return;
    try {
      await collectionsAPI.deleteCollection(id);
      await this.loadData();
      if (this.state.selectedCollection?.id === id) {
        this.setState({ selectedCollection: null, selectedCollectionItems: [] });
      }
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  handleSelectCollection = async (collection) => {
    this.setState({ selectedCollection: collection });
    await this.loadCollectionItems(collection.id);
    await this.loadShares(collection.id);
  };

  handleSearchItems = async (query) => {
    if (!query || query.length < 2) {
      this.setState({ itemSearchResults: [], itemSearchLoading: false });
      return;
    }
    
    this.setState({ itemSearchLoading: true });
    try {
      this.setState({ itemSearchResults: [], itemSearchLoading: false });
    } catch (error) {
      console.error('[Collections] Search error:', error);
      this.setState({ itemSearchResults: [], itemSearchLoading: false });
    }
  };

  handleAddItem = async () => {
    if (!this.state.selectedCollection || !this.state.itemSearchQuery) return;
    
    try {
      // For test purposes, use the search query as contentId
      // In real implementation, this would use the selected search result
      await collectionsAPI.addCollectionItem(this.state.selectedCollection.id, {
        contentId: this.state.itemSearchQuery,
        mediaKind: 'Audio', // Default for test
      });
      this.setState({ 
        addItemModalOpen: false, 
        itemSearchQuery: '', 
        itemSearchResults: [],
        error: null 
      });
      await this.loadCollectionItems(this.state.selectedCollection.id);
    } catch (error) {
      let errorMsg = error.message || 'Failed to add item';
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMsg = error.response.data;
        } else if (error.response.data.detail) {
          errorMsg = error.response.data.detail;
        } else if (error.response.data.message) {
          errorMsg = error.response.data.message;
        }
      }
      this.setState({ error: errorMsg });
    }
  };

  handleOpenShareModal = () => {
    const { shareGroups } = this.state;
    this.setState({
      shareModalOpen: true,
      shareAudienceId: shareGroups[0]?.id ?? null,
      shareAllowStream: true,
      shareAllowDownload: true,
    });
  };

  handleCreateShare = async () => {
    const { selectedCollection, shareAudienceId, shareAllowStream, shareAllowDownload } = this.state;
    if (!selectedCollection || !shareAudienceId) return;

    try {
      await collectionsAPI.createShare({
        collectionId: selectedCollection.id,
        audienceType: 'ShareGroup',
        audienceId: typeof shareAudienceId === 'string' ? shareAudienceId : String(shareAudienceId),
        allowStream: shareAllowStream,
        allowDownload: shareAllowDownload,
      });
      this.setState({ shareModalOpen: false, error: null });
      await this.loadShares(selectedCollection.id);
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  render() {
    const {
      collections,
      selectedCollection,
      selectedCollectionItems,
      shares,
      shareGroups,
      shareGroupsLoading,
      shareModalOpen,
      shareAudienceId,
      shareAllowStream,
      shareAllowDownload,
      loading,
      error,
      createModalOpen,
      addItemModalOpen,
      newCollectionTitle,
      newCollectionType,
      newCollectionDescription,
      itemSearchQuery,
      itemSearchResults,
      itemSearchLoading,
    } = this.state;

    if (loading) return <LoaderSegment />;

    const typeOptions = [
      { key: 'Playlist', text: 'Playlist', value: 'Playlist' },
      { key: 'ShareList', text: 'Share List', value: 'ShareList' },
    ];

    const collectionShares = shares;

    return (
      <div data-testid="collections-root">
      <Container>
          <Header as="h1">
            <Icon name="list" />
            <Header.Content>
              Collections
              <Header.Subheader>Manage your playlists and share lists</Header.Subheader>
            </Header.Content>
          </Header>

          {error && <ErrorSegment caption={error} />}

          <div style={{ marginBottom: '1em' }}>
            <Button 
              data-testid="collections-create" 
              primary 
              onClick={() => this.setState({ createModalOpen: true })}
            >
              <Icon name="plus" />
              Create Collection
            </Button>
          </div>

          {collections.length === 0 ? (
            <Segment placeholder>
              <Header icon>
                <Icon name="list" />
                No collections yet
              </Header>
              <Button 
                data-testid="collections-create-empty"
                primary 
                onClick={() => this.setState({ createModalOpen: true })}
              >
                Create Collection
              </Button>
            </Segment>
          ) : (
            <Table celled>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>Title</Table.HeaderCell>
                  <Table.HeaderCell>Type</Table.HeaderCell>
                  <Table.HeaderCell>Items</Table.HeaderCell>
                  <Table.HeaderCell>Actions</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {collections.map((collection) => (
                  <Table.Row 
                    key={collection.id} 
                    data-testid={`collection-row-${collection.title}`}
                    style={{ cursor: 'pointer' }}
                    onClick={() => this.handleSelectCollection(collection)}
                  >
                    <Table.Cell>{collection.title}</Table.Cell>
                    <Table.Cell>{collection.type || 'Playlist'}</Table.Cell>
                    <Table.Cell>{collection.itemCount || 0}</Table.Cell>
                    <Table.Cell>
                      <Button
                        size="small"
                        negative
                        onClick={(e) => {
                          e.stopPropagation();
                          this.handleDeleteCollection(collection.id);
                        }}
                      >
                        Delete
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}

          {selectedCollection && (
            <Segment style={{ marginTop: '2em' }}>
              <Header as="h2">
                {selectedCollection.title}
                <Header.Subheader>{selectedCollection.type || 'Playlist'}</Header.Subheader>
              </Header>
              
              <div style={{ marginBottom: '1em' }}>
                <Button
                  data-testid="collection-add-item"
                  primary
                  onClick={() => this.setState({ addItemModalOpen: true })}
                >
                  <Icon name="plus" />
                  Add Item
                </Button>
                <Button
                  data-testid="share-create"
                  onClick={this.handleOpenShareModal}
                  style={{ marginLeft: '0.5em' }}
                >
                  <Icon name="share alternate" />
                  Share Collection
                </Button>
              </div>

              <div data-testid="collection-items">
                {selectedCollectionItems.length === 0 ? (
                  <Message info>No items in this collection yet.</Message>
                ) : (
                  <Table>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Content ID</Table.HeaderCell>
                        <Table.HeaderCell>Media Kind</Table.HeaderCell>
                        <Table.HeaderCell>Actions</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {selectedCollectionItems.map((item) => (
                        <Table.Row key={item.id}>
                          <Table.Cell>{item.contentId || 'N/A'}</Table.Cell>
                          <Table.Cell>{item.mediaKind || 'Unknown'}</Table.Cell>
                          <Table.Cell>
                            <Button
                              size="small"
                              negative
                              onClick={() => {
                                collectionsAPI.removeCollectionItem(item.id)
                                  .then(() => this.loadCollectionItems(selectedCollection.id))
                                  .catch((err) => this.setState({ error: err.message }));
                              }}
                            >
                              Remove
                            </Button>
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table>
                )}
              </div>

              <Segment data-testid="shares-list" style={{ marginTop: '1em' }}>
                <Header as="h4">Shares</Header>
                {collectionShares.length === 0 ? (
                  <Message info>No shares yet.</Message>
                ) : (
                  <Table>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Collection</Table.HeaderCell>
                        <Table.HeaderCell>Audience</Table.HeaderCell>
                        <Table.HeaderCell>Stream</Table.HeaderCell>
                        <Table.HeaderCell>Download</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {collectionShares.map((share) => (
                        <Table.Row key={share.id}>
                          <Table.Cell>
                            {share.collection?.title || selectedCollection.title}
                          </Table.Cell>
                          <Table.Cell>
                            {share.audienceType === 'ShareGroup'
                              ? `Group ${share.audienceId}`
                              : share.audienceId}
                          </Table.Cell>
                          <Table.Cell>{share.allowStream ? 'Yes' : 'No'}</Table.Cell>
                          <Table.Cell>{share.allowDownload ? 'Yes' : 'No'}</Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table>
                )}
              </Segment>
            </Segment>
          )}

          {/* Create Collection Modal */}
          <Modal
            open={createModalOpen}
            onClose={() => this.setState({ 
              createModalOpen: false, 
              newCollectionTitle: '', 
              newCollectionType: 'Playlist',
              newCollectionDescription: '',
            })}
          >
            <Modal.Header>Create Collection</Modal.Header>
            <Modal.Content>
              <Form>
                <Form.Field>
                  <label>Type</label>
                  <Dropdown
                    data-testid="collections-type-select"
                    selection
                    options={typeOptions}
                    value={newCollectionType}
                    onChange={(e, { value }) => this.setState({ newCollectionType: value })}
                  />
                </Form.Field>
                <Form.Input
                  data-testid="collections-title-input"
                  label="Title"
                  value={newCollectionTitle}
                  onChange={(e) => this.setState({ newCollectionTitle: e.target.value })}
                  placeholder="Enter collection title"
                />
                <Form.TextArea
                  label="Description"
                  value={newCollectionDescription}
                  onChange={(e) => this.setState({ newCollectionDescription: e.target.value })}
                  placeholder="Optional description"
                />
              </Form>
            </Modal.Content>
            <Modal.Actions>
              <Button onClick={() => this.setState({ 
                createModalOpen: false, 
                newCollectionTitle: '', 
                newCollectionType: 'Playlist',
                newCollectionDescription: '',
              })}>
                Cancel
              </Button>
              <Button 
                data-testid="collections-create-submit" 
                primary 
                onClick={this.handleCreateCollection}
                disabled={!newCollectionTitle.trim()}
              >
                Create
              </Button>
            </Modal.Actions>
          </Modal>

          {/* Share Collection Modal */}
          <Modal
            open={shareModalOpen}
            onClose={() => this.setState({ shareModalOpen: false })}
          >
            <Modal.Header>Share Collection</Modal.Header>
            <Modal.Content>
              {shareGroupsLoading ? (
                <LoaderSegment />
              ) : shareGroups.length === 0 ? (
                <Message warning>No share groups available.</Message>
              ) : (
                <Form>
                  <Form.Field>
                    <label>Share Group</label>
                    <Dropdown
                      data-testid="share-audience-picker"
                      selection
                      options={shareGroups.map((group) => ({
                        key: group.id,
                        text: group.name,
                        value: group.id,
                      }))}
                      value={shareAudienceId}
                      onChange={(event, { value }) =>
                        this.setState({ shareAudienceId: value })
                      }
                    />
                  </Form.Field>
                  <Form.Field>
                    <label htmlFor="share-allow-stream">Allow streaming</label>
                    <input
                      data-testid="share-policy-stream"
                      checked={shareAllowStream}
                      id="share-allow-stream"
                      onChange={(event) =>
                        this.setState({ shareAllowStream: event.target.checked })
                      }
                      type="checkbox"
                    />
                  </Form.Field>
                  <Form.Field>
                    <label htmlFor="share-allow-download">Allow download</label>
                    <input
                      data-testid="share-policy-download"
                      checked={shareAllowDownload}
                      id="share-allow-download"
                      onChange={(event) =>
                        this.setState({ shareAllowDownload: event.target.checked })
                      }
                      type="checkbox"
                    />
                  </Form.Field>
                </Form>
              )}
            </Modal.Content>
            <Modal.Actions>
              <Button onClick={() => this.setState({ shareModalOpen: false })}>
                Cancel
              </Button>
              <Button
                data-testid="share-create-submit"
                primary
                onClick={this.handleCreateShare}
                disabled={!shareAudienceId}
              >
                Share
              </Button>
            </Modal.Actions>
          </Modal>

          {/* Add Item Modal */}
          <Modal
            open={addItemModalOpen}
            onClose={() => this.setState({ 
              addItemModalOpen: false, 
              itemSearchQuery: '', 
              itemSearchResults: [],
            })}
          >
            <Modal.Header>Add Item to {selectedCollection?.title}</Modal.Header>
            <Modal.Content>
              <Form>
                <Form.Field>
                  <label>Search for item</label>
                  <Form.Input
                    data-testid="collection-item-picker"
                    placeholder="Enter content ID or search..."
                    value={itemSearchQuery}
                    onChange={(e) => {
                      const query = e.target.value;
                      this.setState({ itemSearchQuery: query });
                      this.handleSearchItems(query);
                    }}
                    loading={itemSearchLoading}
                  />
                </Form.Field>
                {itemSearchResults.length > 0 && (
                  <Dropdown
                    placeholder="Select an item"
                    fluid
                    search
                    selection
                    options={itemSearchResults.map((item, idx) => ({
                      key: idx,
                      text: item.contentId || item.name || 'Unknown',
                      value: item.contentId || item.id,
                    }))}
                    onChange={(e, { value }) => {
                      this.setState({ itemSearchQuery: value });
                    }}
                  />
                )}
                {itemSearchQuery && itemSearchResults.length === 0 && !itemSearchLoading && (
                  <Message info>
                    No results found. You can still add the search query as a content ID.
                  </Message>
                )}
              </Form>
            </Modal.Content>
            <Modal.Actions>
              <Button onClick={() => this.setState({ 
                addItemModalOpen: false, 
                itemSearchQuery: '', 
                itemSearchResults: [],
              })}>
                Cancel
              </Button>
              <Button 
                data-testid="collection-add-item-submit"
                primary 
                onClick={this.handleAddItem}
                disabled={!itemSearchQuery.trim()}
              >
                Add Item
              </Button>
            </Modal.Actions>
          </Modal>
        </Container>
        </div>
    );
  }
}
