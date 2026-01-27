import * as collectionsAPI from '../../lib/collections';
import * as identityAPI from '../../lib/identity';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import React, { Component } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Container,
  Header,
  Icon,
  Label,
  Modal,
  Segment,
  Table,
} from 'semantic-ui-react';

export default class SharedWithMe extends Component {
  state = {
    backfilling: false,
    backfillResult: null,
    contacts: [],
    error: null,
    loading: true,
    manifest: null,
    manifestLoading: false,
    manifestModalOpen: false,
    selectedShare: null,
    shares: [],
  };

  componentDidMount() {
    this.loadData();
  }

  loadData = async () => {
    try {
      this.setState({ error: null, loading: true });
      const [sharesRes, contactsRes] = await Promise.all([
        collectionsAPI.getShares().catch((error) => {
          // If 401/403/404/400, user isn't authenticated or feature not enabled - return empty list
          if (
            error.response?.status === 401 ||
            error.response?.status === 403 ||
            error.response?.status === 404 ||
            error.response?.status === 400
          ) {
            return { data: [] };
          }

          // For other errors, rethrow to be caught below
          throw error;
        }),
        identityAPI.getContacts().catch(() => ({ data: [] })), // Gracefully handle if Identity not enabled
      ]);

      // Fetch collection details for each share
      const sharesWithCollections = await Promise.all(
        (sharesRes.data || []).map(async (share) => {
          try {
            const collectionRes = await collectionsAPI.getCollection(
              share.collectionId,
            );
            return { ...share, collection: collectionRes.data };
          } catch (error) {
            console.warn(
              'Failed to load collection for share',
              share.id,
              error,
            );
            return share;
          }
        }),
      );

      this.setState({
        contacts: contactsRes.data || [],
        loading: false,
        shares: sharesWithCollections,
      });
    } catch (error) {
      // Only show error if it's not an auth/feature issue (which we handle above)
      const isAuthOrFeatureError =
        error.response?.status === 401 ||
        error.response?.status === 403 ||
        error.response?.status === 404 ||
        error.response?.status === 400;
      this.setState({
        error: isAuthOrFeatureError
          ? null
          : error.response?.data || error.message,
        loading: false,
      });
    }
  };

  getContactNickname = (audienceId, audiencePeerId) => {
    if (audiencePeerId) {
      const contact = this.state.contacts.find(
        (c) => c.peerId === audiencePeerId,
      );
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
      this.setState({
        manifestLoading: true,
        manifestModalOpen: true,
        selectedShare: share,
      });
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

  handleBackfill = async () => {
    const { selectedShare } = this.state;
    if (!selectedShare) return;

    try {
      this.setState({ backfilling: true, backfillResult: null, error: null });
      const result = await collectionsAPI.backfillShare(selectedShare.id);
      this.setState({
        backfilling: false,
        backfillResult: result.data,
      });

      if (result.data.failed === 0) {
        toast.success(result.data.message || 'Backfill started successfully');
      } else {
        toast.warning(
          result.data.message || 'Backfill started with some failures',
        );
      }
    } catch (error) {
      const errorMessage =
        error.response?.data?.message ||
        error.response?.data ||
        error.message ||
        'Failed to start backfill';
      this.setState({
        backfilling: false,
        backfillResult: null,
        error: errorMessage,
      });
      toast.error(errorMessage);
    }
  };

  render() {
    const {
      error,
      loading,
      manifest,
      manifestLoading,
      manifestModalOpen,
      selectedShare,
      shares,
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

        {error && <ErrorSegment caption={error} />}

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
                const displayName =
                  ownerNickname || share.collection?.ownerUserId || 'Unknown';

                return (
                  <Table.Row
                    data-testid={`incoming-share-row-${share.collection?.title || 'Untitled'}`}
                    key={share.id}
                  >
                    <Table.Cell>
                      <strong>{share.collection?.title || 'Untitled'}</strong>
                      {share.collection?.description && (
                        <div
                          style={{
                            color: '#666',
                            fontSize: '0.9em',
                            marginTop: '0.25em',
                          }}
                        >
                          {share.collection.description}
                        </div>
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      {ownerNickname && (
                        <Label
                          color="blue"
                          style={{ marginRight: '0.5em' }}
                        >
                          {ownerNickname}
                        </Label>
                      )}
                      <span>{share.collection?.ownerUserId || 'Unknown'}</span>
                    </Table.Cell>
                    <Table.Cell>
                      {share.collection?.type || 'ShareList'}
                    </Table.Cell>
                    <Table.Cell>
                      {share.allowStream && <Label color="green">Stream</Label>}
                      {share.allowDownload && (
                        <Label color="blue">Download</Label>
                      )}
                      {share.allowReshare && <Label>Reshare</Label>}
                    </Table.Cell>
                    <Table.Cell>
                      <Button
                        data-testid="incoming-share-open"
                        onClick={() => this.handleViewManifest(share)}
                        primary
                        size="small"
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
          onClose={() =>
            this.setState({
              manifest: null,
              manifestModalOpen: false,
              selectedShare: null,
            })
          }
          open={manifestModalOpen}
          size="large"
        >
          <Modal.Header>
            {selectedShare?.collection?.title ||
              manifest?.title ||
              'Collection Contents'}
            {manifest?.ownerContactNickname && (
              <span
                style={{
                  fontSize: '0.8em',
                  fontWeight: 'normal',
                  marginLeft: '1em',
                }}
              >
                by {manifest.ownerContactNickname}
              </span>
            )}
          </Modal.Header>
          <Modal.Content>
            {manifestLoading ? (
              <LoaderSegment />
            ) : manifest ? (
              <div data-testid="shared-manifest">
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
                      {manifest.items.map((item, index) => {
                        // Extract sha256 prefix from contentId (format: "sha256:...")
                        const sha256Prefix =
                          item.contentId?.startsWith('sha256:')
                            ? item.contentId.slice(7, 15) // First 8 chars of hash
                            : item.contentId?.slice(0, 8) || `item-${index}`;
                        return (
                          <Table.Row
                            data-testid={`incoming-item-row-${sha256Prefix}`}
                            key={index}
                          >
                            <Table.Cell>
                              <code style={{ fontSize: '0.85em' }}>
                                {item.fileName ||
                                  item.contentId?.slice(0, 32) ||
                                  'Unknown'}
                              </code>
                            </Table.Cell>
                            <Table.Cell>{item.mediaKind || 'Unknown'}</Table.Cell>
                            <Table.Cell>
                              {item.streamUrl && (
                                <Button
                                  data-testid={`incoming-stream-${sha256Prefix}`}
                                  onClick={() => {
                                    const url = item.streamUrl.startsWith('http')
                                      ? item.streamUrl
                                      : `${window.location.origin}${item.streamUrl}`;
                                    window.open(url, '_blank');
                                  }}
                                  primary
                                  size="small"
                                >
                                  <Icon name="play" />
                                  Stream
                                </Button>
                              )}
                            </Table.Cell>
                          </Table.Row>
                        );
                      })}
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
            {selectedShare?.allowDownload && (
              <Button
                data-testid="incoming-backfill"
                disabled={this.state.backfilling}
                loading={this.state.backfilling}
                onClick={this.handleBackfill}
                primary
              >
                <Icon name="download" />
                Backfill All
              </Button>
            )}
            {this.state.backfillResult && (
              <span
                style={{ color: '#666', fontSize: '0.9em', marginRight: '1em' }}
              >
                {this.state.backfillResult.enqueued} enqueued,{' '}
                {this.state.backfillResult.failed} failed
              </span>
            )}
            <Button
              onClick={() =>
                this.setState({
                  backfillResult: null,
                  manifest: null,
                  manifestModalOpen: false,
                  selectedShare: null,
                })
              }
            >
              Close
            </Button>
          </Modal.Actions>
        </Modal>
      </Container>
    );
  }
}
