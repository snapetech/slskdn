import React, { Component } from 'react';
import {
  Button,
  Container,
  Header,
  Icon,
  Table,
  Label,
  Segment,
  Modal,
} from 'semantic-ui-react';
import * as collectionsAPI from '../../lib/collections';
import * as identityAPI from '../../lib/identity';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';

export default class SharedWithMe extends Component {
  state = {
    shares: [],
    contacts: [],
    loading: true,
    error: null,
    manifestModalOpen: false,
    selectedShare: null,
    manifest: null,
    manifestLoading: false,
  };

  componentDidMount() {
    this.loadData();
  }

  loadData = async () => {
    try {
      this.setState({ loading: true, error: null });
      const [sharesRes, contactsRes] = await Promise.all([
        collectionsAPI.getShares().catch((err) => {
          // If 401/403, user isn't authenticated - return empty list
          if (err.response?.status === 401 || err.response?.status === 403) {
            return { data: [] };
          }
          // For other errors, rethrow to be caught below
          throw err;
        }),
        identityAPI.getContacts().catch(() => ({ data: [] })), // Gracefully handle if Identity not enabled
      ]);
      
      // Fetch collection details for each share
      const sharesWithCollections = await Promise.all(
        (sharesRes.data || []).map(async (share) => {
          try {
            const collectionRes = await collectionsAPI.getCollection(share.collectionId);
            return { ...share, collection: collectionRes.data };
          } catch (err) {
            console.warn('Failed to load collection for share', share.id, err);
            return share;
          }
        })
      );
      
      this.setState({
        shares: sharesWithCollections,
        contacts: contactsRes.data || [],
        loading: false,
      });
    } catch (error) {
      // Only show error if it's not an auth issue (which we handle above)
      const isAuthError = error.response?.status === 401 || error.response?.status === 403;
      this.setState({ 
        error: isAuthError ? null : (error.response?.data || error.message), 
        loading: false 
      });
    }
  };

  getContactNickname = (audienceId, audiencePeerId) => {
    if (audiencePeerId) {
      const contact = this.state.contacts.find(c => c.peerId === audiencePeerId);
      return contact?.nickname || null;
    }
    // For legacy UserId, try to find by matching (this is a best-effort)
    return null;
  };

  getOwnerNickname = (collection) => {
    // Try to get from manifest if available
    if (collection?.ownerContactNickname) {
      return collection.ownerContactNickname;
    }
    // Try to find contact by ownerUserId (best effort)
    if (collection?.ownerUserId) {
      // For now, we can't reliably map UserId to PeerId without additional data
      // This would require storing PeerId in Collection or a lookup table
      return null;
    }
    return null;
  };

  handleViewManifest = async (share) => {
    try {
      this.setState({ manifestModalOpen: true, selectedShare: share, manifestLoading: true });
      const manifestRes = await collectionsAPI.getShareManifest(share.id);
      this.setState({ manifest: manifestRes.data, manifestLoading: false });
    } catch (error) {
      this.setState({ 
        error: error.response?.data || error.message,
        manifestLoading: false,
      });
    }
  };

  handleStreamItem = (contentId, token) => {
    const url = token
      ? `/api/v0/streams/${contentId}?token=${encodeURIComponent(token)}`
      : `/api/v0/streams/${contentId}`;
    window.open(url, '_blank');
  };

  render() {
    const {
      shares,
      loading,
      error,
      manifestModalOpen,
      selectedShare,
      manifest,
      manifestLoading,
    } = this.state;

    if (loading) return <LoaderSegment />;

    return (
      <Container>
        <Header as="h1">
          <Icon name="share" />
          <Header.Content>
            Shared with Me
            <Header.Subheader>Collections shared with you</Header.Subheader>
          </Header.Content>
        </Header>

        {error && <ErrorSegment error={error} />}

        {shares.length === 0 ? (
          <Segment placeholder>
            <Header icon>
              <Icon name="inbox" />
              No shares yet
            </Header>
            <p>Collections shared with you will appear here.</p>
          </Segment>
        ) : (
          <Table>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Collection</Table.HeaderCell>
                <Table.HeaderCell>Shared By</Table.HeaderCell>
                <Table.HeaderCell>Type</Table.HeaderCell>
                <Table.HeaderCell>Permissions</Table.HeaderCell>
                <Table.HeaderCell>Actions</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {shares.map((share) => {
                const ownerNickname = this.getOwnerNickname(share.collection);
                const displayName = ownerNickname || share.collection?.ownerUserId || 'Unknown';
                
                return (
                  <Table.Row key={share.id}>
                    <Table.Cell>
                      <strong>{share.collection?.title || 'Untitled'}</strong>
                      {share.collection?.description && (
                        <div style={{ fontSize: '0.9em', color: '#666', marginTop: '0.25em' }}>
                          {share.collection.description}
                        </div>
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      {ownerNickname && (
                        <Label color="blue" style={{ marginRight: '0.5em' }}>
                          {ownerNickname}
                        </Label>
                      )}
                      <span>{share.collection?.ownerUserId || 'Unknown'}</span>
                    </Table.Cell>
                    <Table.Cell>{share.collection?.type || 'ShareList'}</Table.Cell>
                    <Table.Cell>
                      {share.allowStream && <Label color="green">Stream</Label>}
                      {share.allowDownload && <Label color="blue">Download</Label>}
                      {share.allowReshare && <Label>Reshare</Label>}
                    </Table.Cell>
                    <Table.Cell>
                      <Button
                        size="small"
                        primary
                        onClick={() => this.handleViewManifest(share)}
                      >
                        View Contents
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                );
              })}
            </Table.Body>
          </Table>
        )}

        {/* Manifest Modal */}
        <Modal
          open={manifestModalOpen}
          onClose={() => this.setState({ 
            manifestModalOpen: false, 
            selectedShare: null,
            manifest: null,
          })}
          size="large"
        >
          <Modal.Header>
            {selectedShare?.collection?.title || manifest?.title || 'Collection Contents'}
            {manifest?.ownerContactNickname && (
              <span style={{ fontSize: '0.8em', fontWeight: 'normal', marginLeft: '1em' }}>
                by {manifest.ownerContactNickname}
              </span>
            )}
          </Modal.Header>
          <Modal.Content>
            {manifestLoading ? (
              <LoaderSegment />
            ) : manifest ? (
              <div>
                {manifest.description && (
                  <p style={{ marginBottom: '1em' }}>{manifest.description}</p>
                )}
                {manifest.items && manifest.items.length > 0 ? (
                  <Table>
                    <Table.Header>
                      <Table.Row>
                        <Table.HeaderCell>Content ID</Table.HeaderCell>
                        <Table.HeaderCell>Media Kind</Table.HeaderCell>
                        <Table.HeaderCell>Actions</Table.HeaderCell>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {manifest.items.map((item, idx) => (
                        <Table.Row key={idx}>
                          <Table.Cell>
                            <code style={{ fontSize: '0.85em' }}>
                              {item.contentId.substring(0, 32)}...
                            </code>
                          </Table.Cell>
                          <Table.Cell>{item.mediaKind || 'Unknown'}</Table.Cell>
                          <Table.Cell>
                            {item.streamUrl && (
                              <Button
                                size="small"
                                primary
                                onClick={() => {
                                  const url = item.streamUrl.startsWith('http')
                                    ? item.streamUrl
                                    : `${window.location.origin}${item.streamUrl}`;
                                  window.open(url, '_blank');
                                }}
                              >
                                <Icon name="play" />
                                Stream
                              </Button>
                            )}
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table>
                ) : (
                  <Segment placeholder>
                    <Header icon>
                      <Icon name="file outline" />
                      No items in this collection
                    </Header>
                  </Segment>
                )}
              </div>
            ) : (
              <ErrorSegment error="Failed to load manifest" />
            )}
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ 
              manifestModalOpen: false,
              selectedShare: null,
              manifest: null,
            })}>
              Close
            </Button>
          </Modal.Actions>
        </Modal>
      </Container>
    );
  }
}
