import './Rooms.css';
import React, { useState } from 'react';
import { Button, Header, Icon, Input, Modal, Radio } from 'semantic-ui-react';

const RoomCreateModal = ({ onCreateRoom, ...modalOptions }) => {
  const [open, setOpen] = useState(false);
  const [roomName, setRoomName] = useState('');
  const [isPrivate, setIsPrivate] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleCreate = async () => {
    const name = roomName.trim();
    if (!name) {
      setError('Room name cannot be empty');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await onCreateRoom(name, isPrivate);
      setOpen(false);
      setRoomName('');
      setIsPrivate(false);
    } catch (error) {
      setError(
        error?.response?.data || error?.message || 'Failed to create room',
      );
    } finally {
      setLoading(false);
    }
  };

  const handleKeyPress = (e) => {
    if (e.key === 'Enter') {
      handleCreate();
    }
  };

  return (
    <Modal
      size="small"
      {...modalOptions}
      onClose={() => {
        if (!loading) {
          setOpen(false);
          setError('');
          setRoomName('');
          setIsPrivate(false);
        }
      }}
      onOpen={() => setOpen(true)}
      open={open}
      trigger={
        <Button
          color="green"
          icon
          title="Create New Room"
        >
          <Icon name="plus" />
          Create Room
        </Button>
      }
    >
      <Modal.Header>
        <Icon name="plus" />
        Create New Room
      </Modal.Header>
      <Modal.Content>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <Header as="h4">Room Name</Header>
            <Input
              error={Boolean(error)}
              fluid
              onChange={(_, { value }) => setRoomName(value)}
              onKeyPress={handleKeyPress}
              placeholder="Enter room name..."
              value={roomName}
            />
            {error && (
              <div
                style={{ color: '#db2828', fontSize: '12px', marginTop: '4px' }}
              >
                {error}
              </div>
            )}
          </div>

          <div>
            <Header as="h4">Room Type</Header>
            <div style={{ display: 'flex', gap: '24px' }}>
              <div
                style={{ alignItems: 'center', display: 'flex', gap: '8px' }}
              >
                <Radio
                  checked={!isPrivate}
                  name="roomType"
                  onChange={() => setIsPrivate(false)}
                  value="public"
                />
                <div>
                  <strong>Public Room</strong>
                  <div style={{ color: '#666', fontSize: '12px' }}>
                    Anyone can join and see the room
                  </div>
                </div>
              </div>
              <div
                style={{ alignItems: 'center', display: 'flex', gap: '8px' }}
              >
                <Radio
                  checked={isPrivate}
                  name="roomType"
                  onChange={() => setIsPrivate(true)}
                  value="private"
                />
                <div>
                  <strong>Private Room</strong>
                  <div style={{ color: '#666', fontSize: '12px' }}>
                    Only invited members can join
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div
            style={{
              background: '#f8f9fa',
              borderRadius: '4px',
              padding: '12px',
            }}
          >
            <Icon name="info circle" />
            <strong>Note:</strong> Room creation depends on server permissions.
            Private rooms require server operator approval.
          </div>
        </div>
      </Modal.Content>
      <Modal.Actions>
        <Button
          disabled={loading}
          onClick={() => setOpen(false)}
        >
          Cancel
        </Button>
        <Button
          disabled={!roomName.trim() || loading}
          loading={loading}
          onClick={handleCreate}
          positive
        >
          <Icon name="plus" />
          Create Room
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default RoomCreateModal;
