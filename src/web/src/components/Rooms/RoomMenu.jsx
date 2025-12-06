import './Rooms.css';
import React from 'react';
import { Icon, Menu } from 'semantic-ui-react';

const RoomMenu = ({ active, joined, onRoomChange }) => {
  const names = [...joined];
  const isActive = (name) => active === name;

  return (
    <Menu
      className="room-menu room-tabs"
      size="small"
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
    </Menu>
  );
};

export default RoomMenu;
