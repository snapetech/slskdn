import './Rooms.css';
import React, { useState } from 'react';
import {
  Button,
  Header,
  Icon,
  Input,
  Modal,
  Radio,
} from 'semantic-ui-react';

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
      setError(error?.response?.data || error?.message || 'Failed to create room');
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
      open={open}
      onOpen={() => setOpen(true)}
      onClose={() => {
        if (!loading) {
          setOpen(false);
          setError('');
          setRoomName('');
          setIsPrivate(false);
        }
      }}
      trigger={
        <Button icon color="green" title="Create New Room">
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
              fluid
              placeholder="Enter room name..."
              value={roomName}
              onChange={(_, { value }) => setRoomName(value)}
              onKeyPress={handleKeyPress}
              error={!!error}
            />
            {error && (
              <div style={{ color: '#db2828', fontSize: '12px', marginTop: '4px' }}>
                {error}
              </div>
            )}
          </div>

          <div>
            <Header as="h4">Room Type</Header>
            <div style={{ display: 'flex', gap: '24px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Radio
                  name="roomType"
                  value="public"
                  checked={!isPrivate}
                  onChange={() => setIsPrivate(false)}
                />
                <div>
                  <strong>Public Room</strong>
                  <div style={{ fontSize: '12px', color: '#666' }}>
                    Anyone can join and see the room
                  </div>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Radio
                  name="roomType"
                  value="private"
                  checked={isPrivate}
                  onChange={() => setIsPrivate(true)}
                />
                <div>
                  <strong>Private Room</strong>
                  <div style={{ fontSize: '12px', color: '#666' }}>
                    Only invited members can join
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div style={{ background: '#f8f9fa', padding: '12px', borderRadius: '4px' }}>
            <Icon name="info circle" />
            <strong>Note:</strong> Room creation depends on server permissions.
            Private rooms require server operator approval.
          </div>
        </div>
      </Modal.Content>
      <Modal.Actions>
        <Button
          onClick={() => setOpen(false)}
          disabled={loading}
        >
          Cancel
        </Button>
        <Button
          positive
          loading={loading}
          disabled={!roomName.trim() || loading}
          onClick={handleCreate}
        >
          <Icon name="plus" />
          Create Room
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default RoomCreateModal;

