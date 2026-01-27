import * as identityAPI from '../../lib/identity';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import React, { Component } from 'react';
import {
  Button,
  Card,
  Container,
  Header,
  Icon,
  Label,
  List,
  Modal,
  Segment,
  Tab,
  Table,
} from 'semantic-ui-react';

export default class Contacts extends Component {
  state = {
    activeTab: 0,
    addFriendModalOpen: false,
    contacts: [],
    createInviteModalOpen: false,
    error: null,
    inviteFriendCode: null,
    inviteLink: null,
    loading: true,
    nearby: [],
    nearbyLoading: false,
  };

  componentDidMount() {
    this.loadContacts();
    this.loadNearby();
  }

  loadContacts = async () => {
    try {
      this.setState({ error: null, loading: true });
      const response = await identityAPI.getContacts();
      this.setState({ contacts: response.data || [], loading: false });
    } catch (error) {
      // If 401/403/404, feature not enabled or not authenticated - return empty list
      if (
        error.response?.status === 401 ||
        error.response?.status === 403 ||
        error.response?.status === 404
      ) {
        this.setState({ contacts: [], error: null, loading: false });
      } else {
        this.setState({ error: error.message, loading: false });
      }
    }
  };

  loadNearby = async () => {
    try {
      this.setState({ nearbyLoading: true });
      const response = await identityAPI.getNearby();
      this.setState({ nearby: response.data || [], nearbyLoading: false });
    } catch {
      this.setState({ nearbyLoading: false });
      // Nearby may fail if mDNS not available, don't show error
    }
  };

  handleAddFromInvite = async (inviteLink, nickname) => {
    try {
      await identityAPI.addContactFromInvite({ inviteLink, nickname });
      this.setState({ addFriendModalOpen: false });
      await this.loadContacts();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  handleAddFromDiscovery = async (peerId, nickname) => {
    try {
      await identityAPI.addContactFromDiscovery({ nickname, peerId });
      await this.loadContacts();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  handleCreateInvite = async () => {
    try {
      const response = await identityAPI.createInvite({ expiresInHours: 24 });
      this.setState({
        createInviteModalOpen: true,
        error: null,
        inviteFriendCode: response.data.friendCode,
        inviteLink: response.data.inviteLink,
      });
    } catch (error) {
      console.error('[Contacts] Create invite error:', error);
      // Extract error message from response (supports ProblemDetails, object with message/error, or string)
      let errorMessage = error.message || 'Failed to create invite';
      if (error.response) {
        const status = error.response.status;
        const url = error.response.config?.url || 'unknown';
        const contentLength = error.response.headers['content-length'];

        console.error('[Contacts] Response status:', status);
        console.error('[Contacts] Response URL:', url);
        console.error('[Contacts] Response data:', error.response.data);

        // Check for empty body
        if (contentLength === '0' || contentLength === 0) {
          console.error(
            `[Contacts] HTTP ${status} with empty body from ${url}`,
          );
        }

        if (error.response.data) {
          if (typeof error.response.data === 'string') {
            errorMessage = error.response.data;
          } else if (error.response.data.detail) {
            errorMessage = error.response.data.detail; // ProblemDetails format
          } else if (error.response.data.message) {
            errorMessage = error.response.data.message;
          } else if (error.response.data.error) {
            errorMessage = error.response.data.error;
          } else if (error.response.data.title) {
            errorMessage = error.response.data.title; // ProblemDetails title as fallback
          } else {
            errorMessage = JSON.stringify(error.response.data);
          }
        } else if (status === 400) {
          // 400 with empty body likely means CSRF validation failed or user identity missing
          errorMessage =
            'Request failed. This may be due to: missing CSRF token (try refreshing the page), user identity not available (configure Soulseek username or enable Identity & Friends), or invalid input.';
        } else if (status === 401) {
          errorMessage = 'Authentication required. Please refresh the page.';
        } else if (status === 403) {
          errorMessage = 'Not authorized.';
        } else if (status === 404) {
          // 404 could be route mismatch (double prefix bug) or feature disabled
          if (url.includes('/api/v0/api/v0')) {
            errorMessage = `Endpoint not found: ${url} (possible route mismatch - check browser console)`;
          } else {
            errorMessage =
              'Identity & Friends feature is not enabled, or endpoint not found.';
          }
        } else if (status >= 500) {
          errorMessage = 'Server error. Please check server logs.';
        }
      }

      this.setState({
        createInviteModalOpen: false,
        error:
          errorMessage ||
          'Failed to create invite. Please ensure Identity & Friends is enabled and configured.',
      });
    }
  };

  handleDeleteContact = async (id) => {
    if (!window.confirm('Delete this contact?')) return;
    try {
      await identityAPI.deleteContact(id);
      await this.loadContacts();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  render() {
    const {
      activeTab,
      addFriendModalOpen,
      contacts,
      createInviteModalOpen,
      error,
      inviteFriendCode,
      inviteLink,
      loading,
      nearby,
      nearbyLoading,
    } = this.state;

    const panes = [
      {
        menuItem: 'All Contacts',
        render: () => (
          <Tab.Pane>
            {loading ? (
              <LoaderSegment />
            ) : contacts.length === 0 ? (
              <Segment placeholder>
                <Header icon>
                  <Icon name="users" />
                  No contacts yet
                </Header>
                <Button
                  as="button"
                  data-testid="contacts-create-invite-empty"
                  onClick={this.handleCreateInvite}
                  primary
                >
                  Create Invite
                </Button>
              </Segment>
            ) : (
              <Table>
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell>Nickname</Table.HeaderCell>
                    <Table.HeaderCell>Peer ID</Table.HeaderCell>
                    <Table.HeaderCell>Verified</Table.HeaderCell>
                    <Table.HeaderCell>Last Seen</Table.HeaderCell>
                    <Table.HeaderCell>Actions</Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {contacts.map((contact) => (
                    <Table.Row
                      data-testid={`contact-row-${contact.nickname || contact.peerId.slice(0, 8)}`}
                      key={contact.id}
                    >
                      <Table.Cell>{contact.nickname || 'Unnamed'}</Table.Cell>
                      <Table.Cell>
                        <code style={{ fontSize: '0.85em' }}>
                          {contact.peerId.slice(0, 16)}...
                        </code>
                      </Table.Cell>
                      <Table.Cell>
                        {contact.verified ? (
                          <Label color="green">Verified</Label>
                        ) : (
                          <Label>Unverified</Label>
                        )}
                      </Table.Cell>
                      <Table.Cell>
                        {contact.lastSeen
                          ? new Date(contact.lastSeen).toLocaleString()
                          : 'Never'}
                      </Table.Cell>
                      <Table.Cell>
                        <Button
                          negative
                          onClick={() => this.handleDeleteContact(contact.id)}
                          size="small"
                        >
                          Delete
                        </Button>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Tab.Pane>
        ),
      },
      {
        menuItem: 'Nearby',
        render: () => (
          <Tab.Pane>
            {nearbyLoading ? (
              <LoaderSegment />
            ) : nearby.length === 0 ? (
              <Segment placeholder>
                <Header icon>
                  <Icon name="wifi" />
                  No nearby peers found
                </Header>
                <p>Make sure you're on the same network and mDNS is working.</p>
                <Button onClick={this.loadNearby}>Refresh</Button>
              </Segment>
            ) : (
              <List
                divided
                relaxed
              >
                {nearby.map((peer, index) => (
                  <List.Item key={index}>
                    <List.Content>
                      <List.Header>{peer.displayName}</List.Header>
                      <List.Description>
                        Code: <code>{peer.peerCode}</code>
                        <br />
                        Endpoint: {peer.endpoint}
                      </List.Description>
                      <Button
                        onClick={() => {
                          const nickname = prompt(
                            'Enter nickname for this contact:',
                          );
                          if (nickname) {
                            this.handleAddFromDiscovery(peer.peerId, nickname);
                          }
                        }}
                        primary
                        size="small"
                        style={{ marginTop: '0.5em' }}
                      >
                        Add Contact
                      </Button>
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            )}
          </Tab.Pane>
        ),
      },
    ];

    return (
      <div data-testid="contacts-root">
      <Container>
        <Header as="h1">
          <Icon name="address book" />
          <Header.Content>
            Contacts
            <Header.Subheader>Manage your peer contacts</Header.Subheader>
          </Header.Content>
        </Header>

        {error && <ErrorSegment caption={error} />}

        <div style={{ marginBottom: '1em' }}>
          {/* Always render Create Invite button - not conditional on loading state */}
          <Button
            as="button"
            data-testid="contacts-create-invite"
            onClick={this.handleCreateInvite}
            primary
          >
            <Icon name="plus" />
            Create Invite
          </Button>
          <Button
            as="button"
            data-testid="contacts-add-friend"
            onClick={() => this.setState({ addFriendModalOpen: true })}
          >
            <Icon name="user plus" />
            Add Friend
          </Button>
          <Button onClick={this.loadNearby}>
            <Icon name="refresh" />
            Refresh Nearby
          </Button>
        </div>

        <Tab
          activeIndex={activeTab}
          onTabChange={(e, { activeIndex }) =>
            this.setState({ activeTab: activeIndex })
          }
          panes={panes}
        />

        {/* Add Friend Modal */}
        <Modal
          onClose={() => this.setState({ addFriendModalOpen: false })}
          open={addFriendModalOpen}
        >
          <Modal.Header>Add Friend from Invite</Modal.Header>
          <Modal.Content>
            <p>Paste an invite link:</p>
            <AddFriendForm
              onSubmit={(inviteLink, nickname) => {
                this.handleAddFromInvite(inviteLink, nickname);
              }}
            />
          </Modal.Content>
        </Modal>

        {/* Create Invite Modal */}
        <Modal
          onClose={() => this.setState({ createInviteModalOpen: false })}
          open={createInviteModalOpen}
        >
          <Modal.Header>Invite Created</Modal.Header>
          <Modal.Content>
            <p>Share this invite link:</p>
            <div style={{ marginBottom: '1em' }}>
              <input
                data-testid="contacts-invite-output"
                onClick={(e) => e.target.select()}
                readOnly
                style={{ padding: '0.5em', width: '100%' }}
                value={inviteLink || ''}
              />
            </div>
            {inviteFriendCode && (
              <p>
                Friend Code:{' '}
                <code data-testid="contacts-invite-friend-code">
                  {inviteFriendCode}
                </code>
              </p>
            )}
            <p>
              <small>QR code display coming soon</small>
            </p>
          </Modal.Content>
          <Modal.Actions>
            <Button
              onClick={() => this.setState({ createInviteModalOpen: false })}
            >
              Close
            </Button>
          </Modal.Actions>
        </Modal>
      </Container>
      </div>
    );
  }
}

class AddFriendForm extends Component {
  state = { inviteLink: '', nickname: '' };

  handleSubmit = (e) => {
    e.preventDefault();
    if (this.state.inviteLink && this.state.nickname) {
      this.props.onSubmit(this.state.inviteLink, this.state.nickname);
    }
  };

  render() {
    return (
      <form onSubmit={this.handleSubmit}>
        <div style={{ marginBottom: '1em' }}>
          <label>Invite Link:</label>
          <input
            data-testid="contacts-add-invite-input"
            onChange={(e) => this.setState({ inviteLink: e.target.value })}
            placeholder="slskdn://invite/..."
            style={{ padding: '0.5em', width: '100%' }}
            type="text"
            value={this.state.inviteLink}
          />
        </div>
        <div style={{ marginBottom: '1em' }}>
          <label>Nickname:</label>
          <input
            data-testid="contacts-contact-nickname"
            onChange={(e) => this.setState({ nickname: e.target.value })}
            placeholder="Friend's name"
            style={{ padding: '0.5em', width: '100%' }}
            type="text"
            value={this.state.nickname}
          />
        </div>
        <Button
          data-testid="contacts-add-invite-submit"
          primary
          type="submit"
        >
          Add Contact
        </Button>
      </form>
    );
  }
}
