import './Rooms.css';
import RoomJoinModal from './RoomJoinModal';
import React, { useState } from 'react';
import { Button, Icon, Input, Menu } from 'semantic-ui-react';

const RoomMenu = ({ active, joinRoom, joined, onRoomChange, ...rest }) => {
  const [roomInput, setRoomInput] = useState('');
  const names = [...joined];
  const isActive = (name) => active === name;

  const handleJoinDirect = () => {
    if (roomInput.trim()) {
      joinRoom(roomInput.trim());
      setRoomInput('');
    }
  };

  return (
    <Menu
      className="room-menu"
      size="large"
    >
      {names.map((name) => (
        <Menu.Item
          active={isActive(name)}
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={name}
          name={name}
          onClick={() => onRoomChange(name)}
        >
          <Icon
            color="green"
            name="circle"
            size="tiny"
          />
          {name}
        </Menu.Item>
      ))}
      <Menu.Menu position="right">
        <Menu.Item>
          <Input
            action={{
              icon: 'sign-in',
              onClick: handleJoinDirect,
            }}
            onChange={(event) => setRoomInput(event.target.value)}
            onKeyUp={(event) => event.key === 'Enter' && handleJoinDirect()}
            placeholder="Room name..."
            size="small"
            value={roomInput}
          />
        </Menu.Item>
        <RoomJoinModal
          centered
          joinRoom={joinRoom}
          size="small"
          trigger={
            <Button
              className="add-button"
              icon
              title="Browse rooms"
            >
              <Icon name="list" />
            </Button>
          }
          {...rest}
        />
      </Menu.Menu>
    </Menu>
  );
};

export default RoomMenu;
