import * as userNotes from '../../lib/userNotes';
import React, { useEffect, useState } from 'react';
import { Button, Form, Header, Icon, Modal } from 'semantic-ui-react';

const colors = [
  { icon: 'circle outline', text: 'None', value: null },
  {
    label: { circular: true, color: 'red', empty: true },
    text: 'Red',
    value: 'red',
  },
  {
    label: { circular: true, color: 'orange', empty: true },
    text: 'Orange',
    value: 'orange',
  },
  {
    label: { circular: true, color: 'yellow', empty: true },
    text: 'Yellow',
    value: 'yellow',
  },
  {
    label: { circular: true, color: 'olive', empty: true },
    text: 'Olive',
    value: 'olive',
  },
  {
    label: { circular: true, color: 'green', empty: true },
    text: 'Green',
    value: 'green',
  },
  {
    label: { circular: true, color: 'teal', empty: true },
    text: 'Teal',
    value: 'teal',
  },
  {
    label: { circular: true, color: 'blue', empty: true },
    text: 'Blue',
    value: 'blue',
  },
  {
    label: { circular: true, color: 'violet', empty: true },
    text: 'Violet',
    value: 'violet',
  },
  {
    label: { circular: true, color: 'purple', empty: true },
    text: 'Purple',
    value: 'purple',
  },
  {
    label: { circular: true, color: 'pink', empty: true },
    text: 'Pink',
    value: 'pink',
  },
  {
    label: { circular: true, color: 'brown', empty: true },
    text: 'Brown',
    value: 'brown',
  },
  {
    label: { circular: true, color: 'grey', empty: true },
    text: 'Grey',
    value: 'grey',
  },
  {
    label: { circular: true, color: 'black', empty: true },
    text: 'Black',
    value: 'black',
  },
];

const UserNoteModal = ({ onClose, trigger, username }) => {
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState('');
  const [color, setColor] = useState(null);
  const [isHighPriority, setIsHighPriority] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const fetchNote = async () => {
      if (!username || !open) return;

      setLoading(true);
      try {
        const response = await userNotes.getNote({ username });
        if (response.data) {
          setNote(response.data.note || '');
          setColor(response.data.color || null);
          setIsHighPriority(response.data.isHighPriority || false);
        } else {
          // Reset if no note exists
          setNote('');
          setColor(null);
          setIsHighPriority(false);
        }
      } catch (error) {
        if (error.response && error.response.status === 404) {
          setNote('');
          setColor(null);
          setIsHighPriority(false);
        } else {
          console.error('Failed to fetch user note', error);
        }
      } finally {
        setLoading(false);
      }
    };

    fetchNote();
  }, [open, username]);

  const handleClose = () => {
    setOpen(false);
    if (onClose) onClose();
  };

  const handleSave = async () => {
    setLoading(true);
    try {
      await userNotes.setNote({
        color,
        isHighPriority,
        note,
        username,
      });
      handleClose();
    } catch (error) {
      console.error('Failed to save user note', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      onClose={handleClose}
      onOpen={() => setOpen(true)}
      open={open}
      size="tiny"
      trigger={trigger}
    >
      <Header>
        <Icon name="sticky note outline" />
        <Modal.Content>User Note: {username}</Modal.Content>
      </Header>
      <Modal.Content>
        <Form loading={loading}>
          <Form.Dropdown
            fluid
            label="Color Rating"
            onChange={(_, { value }) => setColor(value)}
            options={colors}
            placeholder="Select Color"
            selection
            value={color}
          />
          <Form.TextArea
            label="Note"
            onChange={(event) => setNote(event.target.value)}
            placeholder="Enter notes about this user..."
            rows={5}
            value={note}
          />
          <Form.Checkbox
            checked={isHighPriority}
            label="High Priority (Warning)"
            onChange={(_, { checked }) => setIsHighPriority(checked)}
          />
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button
          disabled={loading}
          onClick={handleSave}
          primary
        >
          Save
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default UserNoteModal;
