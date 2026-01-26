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
import * as identityAPI from '../../lib/identity';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';

export default class Contacts extends Component {
  state = {
    contacts: [],
    nearby: [],
    error: null,
    loading: true,
    nearbyLoading: false,
    activeTab: 0,
    addFriendModalOpen: false,
    createInviteModalOpen: false,
    inviteLink: null,
    inviteFriendCode: null,
  };

  componentDidMount() {
    this.loadContacts();
    this.loadNearby();
  }

  loadContacts = async () => {
    try {
      this.setState({ loading: true, error: null });
      const response = await identityAPI.getContacts();
      this.setState({ contacts: response.data || [], loading: false });
    } catch (error) {
      // If 401/403/404, feature not enabled or not authenticated - return empty list
      if (error.response?.status === 401 || error.response?.status === 403 || error.response?.status === 404) {
        this.setState({ contacts: [], loading: false, error: null });
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
    } catch (error) {
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
      await identityAPI.addContactFromDiscovery({ peerId, nickname });
      await this.loadContacts();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  handleCreateInvite = async () => {
    try {
      const response = await identityAPI.createInvite({ expiresInHours: 24 });
      this.setState({
        inviteLink: response.data.inviteLink,
        inviteFriendCode: response.data.friendCode,
        createInviteModalOpen: true,
      });
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
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
      contacts,
      nearby,
      error,
      loading,
      nearbyLoading,
      activeTab,
      addFriendModalOpen,
      createInviteModalOpen,
      inviteLink,
      inviteFriendCode,
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
                <Button primary onClick={this.handleCreateInvite}>
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
                    <Table.Row key={contact.id}>
                      <Table.Cell>{contact.nickname || 'Unnamed'}</Table.Cell>
                      <Table.Cell>
                        <code style={{ fontSize: '0.85em' }}>
                          {contact.peerId.substring(0, 16)}...
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
                          size="small"
                          negative
                          onClick={() => this.handleDeleteContact(contact.id)}
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
              <List divided relaxed>
                {nearby.map((peer, idx) => (
                  <List.Item key={idx}>
                    <List.Content>
                      <List.Header>{peer.displayName}</List.Header>
                      <List.Description>
                        Code: <code>{peer.peerCode}</code>
                        <br />
                        Endpoint: {peer.endpoint}
                      </List.Description>
                      <Button
                        size="small"
                        primary
                        style={{ marginTop: '0.5em' }}
                        onClick={() => {
                          const nickname = prompt('Enter nickname for this contact:');
                          if (nickname) {
                            this.handleAddFromDiscovery(peer.peerId, nickname);
                          }
                        }}
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
      <Container>
        <Header as="h1">
          <Icon name="address book" />
          <Header.Content>
            Contacts
            <Header.Subheader>Manage your peer contacts</Header.Subheader>
          </Header.Content>
        </Header>

        {error && <ErrorSegment error={error} />}

        <div style={{ marginBottom: '1em' }}>
          <Button primary onClick={this.handleCreateInvite}>
            <Icon name="plus" />
            Create Invite
          </Button>
          <Button onClick={() => this.setState({ addFriendModalOpen: true })}>
            <Icon name="user plus" />
            Add Friend
          </Button>
          <Button onClick={this.loadNearby}>
            <Icon name="refresh" />
            Refresh Nearby
          </Button>
        </div>

        <Tab panes={panes} activeIndex={activeTab} onTabChange={(e, { activeIndex }) => this.setState({ activeTab: activeIndex })} />

        {/* Add Friend Modal */}
        <Modal
          open={addFriendModalOpen}
          onClose={() => this.setState({ addFriendModalOpen: false })}
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
          open={createInviteModalOpen}
          onClose={() => this.setState({ createInviteModalOpen: false })}
        >
          <Modal.Header>Invite Created</Modal.Header>
          <Modal.Content>
            <p>Share this invite link:</p>
            <div style={{ marginBottom: '1em' }}>
              <input
                readOnly
                value={inviteLink || ''}
                style={{ width: '100%', padding: '0.5em' }}
                onClick={(e) => e.target.select()}
              />
            </div>
            {inviteFriendCode && (
              <p>
                Friend Code: <code>{inviteFriendCode}</code>
              </p>
            )}
            <p>
              <small>QR code display coming soon</small>
            </p>
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ createInviteModalOpen: false })}>
              Close
            </Button>
          </Modal.Actions>
        </Modal>
      </Container>
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
            type="text"
            value={this.state.inviteLink}
            onChange={(e) => this.setState({ inviteLink: e.target.value })}
            placeholder="slskdn://invite/..."
            style={{ width: '100%', padding: '0.5em' }}
          />
        </div>
        <div style={{ marginBottom: '1em' }}>
          <label>Nickname:</label>
          <input
            type="text"
            value={this.state.nickname}
            onChange={(e) => this.setState({ nickname: e.target.value })}
            placeholder="Friend's name"
            style={{ width: '100%', padding: '0.5em' }}
          />
        </div>
        <Button type="submit" primary>
          Add Contact
        </Button>
      </form>
    );
  }
}
