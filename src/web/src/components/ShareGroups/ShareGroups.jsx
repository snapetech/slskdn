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
import * as identityAPI from '../../lib/identity';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';

export default class ShareGroups extends Component {
  state = {
    shareGroups: [],
    contacts: [],
    loading: true,
    error: null,
    createModalOpen: false,
    addMemberModalOpen: false,
    selectedGroup: null,
    newGroupName: '',
    selectedContactId: null,
    selectedUserId: null,
    usePeerId: true,
  };

  componentDidMount() {
    this.loadData();
  }

  loadData = async () => {
    try {
      this.setState({ loading: true, error: null });
      const [groupsRes, contactsRes] = await Promise.all([
        collectionsAPI.getShareGroups().catch((err) => {
          // If 401/403/404, feature not enabled or not authenticated - return empty list
          // 400 errors might have useful messages, so let them through
          if (err.response?.status === 401 || err.response?.status === 403 || 
              err.response?.status === 404) {
            return { data: [] };
          }
          throw err;
        }),
        identityAPI.getContacts().catch(() => ({ data: [] })), // Gracefully handle if Identity not enabled
      ]);
      this.setState({
        shareGroups: groupsRes.data || [],
        contacts: contactsRes.data || [],
        loading: false,
      });
    } catch (error) {
      // Extract error message from response
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
      // Only suppress errors for 401/403/404 (auth/feature disabled)
      const isAuthOrFeatureError = error.response?.status === 401 || 
                                   error.response?.status === 403 || 
                                   error.response?.status === 404;
      this.setState({ 
        error: isAuthOrFeatureError ? null : errorMsg, 
        loading: false 
      });
    }
  };

  handleCreateGroup = async () => {
    try {
      await collectionsAPI.createShareGroup({ name: this.state.newGroupName });
      this.setState({ createModalOpen: false, newGroupName: '', error: null });
      await this.loadData();
    } catch (error) {
      console.error('[ShareGroups] Create group error:', error);
      // Extract error message from response (supports ProblemDetails, object with message/error, or string)
      let errorMsg = error.message || 'Failed to create share group';
      if (error.response) {
        const status = error.response.status;
        const url = error.response.config?.url || 'unknown';
        const contentLength = error.response.headers['content-length'];
        
        console.error('[ShareGroups] Response status:', status);
        console.error('[ShareGroups] Response URL:', url);
        console.error('[ShareGroups] Response data:', error.response.data);
        
        // Check for empty body
        if (contentLength === '0' || contentLength === 0) {
          console.error(`[ShareGroups] HTTP ${status} with empty body from ${url}`);
        }
        
        if (error.response.data) {
          if (typeof error.response.data === 'string') {
            errorMsg = error.response.data;
          } else if (error.response.data.detail) {
            errorMsg = error.response.data.detail; // ProblemDetails format
          } else if (error.response.data.message) {
            errorMsg = error.response.data.message;
          } else if (error.response.data.error) {
            errorMsg = error.response.data.error;
          } else if (error.response.data.title) {
            errorMsg = error.response.data.title; // ProblemDetails title as fallback
          } else {
            errorMsg = JSON.stringify(error.response.data);
          }
        } else if (status === 400) {
          // 400 with empty body likely means CSRF validation failed or user identity missing
          errorMsg = 'Request failed. This may be due to: missing CSRF token (try refreshing the page), user identity not available (configure Soulseek username or enable Identity & Friends), or invalid input.';
        } else if (status === 401) {
          errorMsg = 'Authentication required. Please refresh the page.';
        } else if (status === 403) {
          errorMsg = 'Not authorized.';
        } else if (status === 404) {
          // 404 could be route mismatch (double prefix bug) or feature disabled
          if (url.includes('/api/v0/api/v0')) {
            errorMsg = `Endpoint not found: ${url} (possible route mismatch - check browser console)`;
          } else {
            errorMsg = 'Collections sharing feature is not enabled, or endpoint not found.';
          }
        } else if (status >= 500) {
          errorMsg = 'Server error. Please check server logs.';
        }
      }
      this.setState({ error: errorMsg || 'Failed to create share group. Please configure Soulseek username or enable Identity & Friends.' });
    }
  };

  handleAddMember = async () => {
    if (!this.state.selectedGroup) return;
    
    try {
      const data = this.state.usePeerId && this.state.selectedContactId
        ? { peerId: this.state.selectedContactId }
        : { userId: this.state.selectedUserId || this.state.selectedContactId };
      
      await collectionsAPI.addShareGroupMember(this.state.selectedGroup.id, data);
      this.setState({ addMemberModalOpen: false, selectedContactId: null, selectedUserId: null, error: null });
      await this.loadData();
    } catch (error) {
      // Extract error message from response (supports ProblemDetails, object with message/error, or string)
      let errorMsg = error.message;
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMsg = error.response.data;
        } else if (error.response.data.detail) {
          errorMsg = error.response.data.detail; // ProblemDetails format
        } else if (error.response.data.message) {
          errorMsg = error.response.data.message;
        } else if (error.response.data.error) {
          errorMsg = error.response.data.error;
        } else if (error.response.data.title) {
          errorMsg = error.response.data.title; // ProblemDetails title as fallback
        } else {
          errorMsg = JSON.stringify(error.response.data);
        }
      }
      this.setState({ error: errorMsg || 'Failed to add member. Please configure Soulseek username or enable Identity & Friends.' });
    }
  };

  handleDeleteGroup = async (id) => {
    if (!window.confirm('Delete this share group?')) return;
    try {
      await collectionsAPI.deleteShareGroup(id);
      await this.loadData();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  handleRemoveMember = async (groupId, userId) => {
    if (!window.confirm('Remove this member?')) return;
    try {
      await collectionsAPI.removeShareGroupMember(groupId, userId);
      await this.loadData();
    } catch (error) {
      this.setState({ error: error.response?.data || error.message });
    }
  };

  render() {
    const {
      shareGroups,
      contacts,
      loading,
      error,
      createModalOpen,
      addMemberModalOpen,
      selectedGroup,
      newGroupName,
      selectedContactId,
      selectedUserId,
      usePeerId,
    } = this.state;

    const contactOptions = contacts.map(c => ({
      key: c.id,
      text: `${c.nickname || 'Unnamed'} (${c.peerId?.substring(0, 16)}...)`,
      value: c.peerId,
      contact: c,
    }));

    if (loading) return <LoaderSegment />;

    return (
      <Container>
        <Header as="h1">
          <Icon name="users" />
          <Header.Content>
            Share Groups
            <Header.Subheader>Manage groups for sharing collections</Header.Subheader>
          </Header.Content>
        </Header>

        {error && <ErrorSegment caption={error} />}

        <div style={{ marginBottom: '1em' }}>
          <Button data-testid="groups-create" primary onClick={() => this.setState({ createModalOpen: true })}>
            <Icon name="plus" />
            Create Group
          </Button>
        </div>

        {shareGroups.length === 0 ? (
          <Segment placeholder>
            <Header icon>
              <Icon name="users" />
              No share groups yet
            </Header>
            <Button primary onClick={() => this.setState({ createModalOpen: true })}>
              Create Your First Group
            </Button>
          </Segment>
        ) : (
          <Table>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Name</Table.HeaderCell>
                <Table.HeaderCell>Members</Table.HeaderCell>
                <Table.HeaderCell>Created</Table.HeaderCell>
                <Table.HeaderCell>Actions</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {shareGroups.map((group) => (
                <Table.Row key={group.id} data-testid={`group-row-${group.name}`}>
                  <Table.Cell>{group.name}</Table.Cell>
                  <Table.Cell>
                    <Button
                      size="small"
                      onClick={async () => {
                        try {
                          const membersRes = await collectionsAPI.getShareGroupMembers(group.id, true);
                          const members = membersRes.data || [];
                          alert(`Members:\n${members.map(m => 
                            m.contactNickname || m.userId
                          ).join('\n')}`);
                        } catch (err) {
                          console.error(err);
                        }
                      }}
                    >
                      View Members
                    </Button>
                  </Table.Cell>
                  <Table.Cell>{new Date(group.createdAt).toLocaleDateString()}</Table.Cell>
                  <Table.Cell>
                    <Button
                      data-testid="group-add-member"
                      size="small"
                      primary
                      onClick={() => this.setState({ addMemberModalOpen: true, selectedGroup: group })}
                    >
                      Add Member
                    </Button>
                    <Button
                      size="small"
                      negative
                      onClick={() => this.handleDeleteGroup(group.id)}
                    >
                      Delete
                    </Button>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}

        {/* Create Group Modal */}
        <Modal
          open={createModalOpen}
          onClose={() => this.setState({ createModalOpen: false, newGroupName: '' })}
        >
          <Modal.Header>Create Share Group</Modal.Header>
          <Modal.Content>
            <Form>
              <Form.Input
                data-testid="groups-name-input"
                label="Group Name"
                value={newGroupName}
                onChange={(e) => this.setState({ newGroupName: e.target.value })}
                placeholder="Enter group name"
              />
            </Form>
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ createModalOpen: false, newGroupName: '' })}>
              Cancel
            </Button>
            <Button data-testid="groups-create-submit" primary onClick={this.handleCreateGroup} disabled={!newGroupName.trim()}>
              Create
            </Button>
          </Modal.Actions>
        </Modal>

        {/* Add Member Modal */}
        <Modal
          open={addMemberModalOpen}
          onClose={() => this.setState({ 
            addMemberModalOpen: false, 
            selectedGroup: null,
            selectedContactId: null,
            selectedUserId: null,
          })}
        >
          <Modal.Header>Add Member to {selectedGroup?.name}</Modal.Header>
          <Modal.Content>
            {contacts.length > 0 ? (
              <Form>
                <Form.Field>
                  <label>Add from Contacts</label>
                  <Dropdown
                    data-testid="group-member-picker"
                    placeholder="Select a contact"
                    fluid
                    search
                    selection
                    options={contactOptions}
                    value={selectedContactId}
                    onChange={(e, { value }) => this.setState({ 
                      selectedContactId: value,
                      usePeerId: true,
                    })}
                  />
                </Form.Field>
                <Message info>
                  <p>Or enter a Soulseek username (legacy):</p>
                  <Form.Input
                    placeholder="Soulseek username"
                    value={selectedUserId}
                    onChange={(e) => this.setState({ 
                      selectedUserId: e.target.value,
                      usePeerId: false,
                    })}
                  />
                </Message>
              </Form>
            ) : (
              <Form>
                <Form.Field>
                  <label>Soulseek Username (legacy)</label>
                  <Form.Input
                    placeholder="Enter username"
                    value={selectedUserId}
                    onChange={(e) => this.setState({ selectedUserId: e.target.value })}
                  />
                </Form.Field>
                <Message warning>
                  No contacts available. Add contacts from the Contacts page to use friend-based sharing.
                </Message>
              </Form>
            )}
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ 
              addMemberModalOpen: false,
              selectedGroup: null,
              selectedContactId: null,
              selectedUserId: null,
            })}>
              Cancel
            </Button>
            <Button 
              data-testid="group-member-add-submit"
              primary 
              onClick={this.handleAddMember}
              disabled={!selectedContactId && !selectedUserId}
            >
              Add Member
            </Button>
          </Modal.Actions>
        </Modal>
      </Container>
    );
  }
}
