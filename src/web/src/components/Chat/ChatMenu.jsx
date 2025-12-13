import './Chat.css';
import React from 'react';
import { Icon, Label, Menu } from 'semantic-ui-react';

const ChatMenu = ({ active, conversations, onConversationChange, tabOrder }) => {
  const names =
    tabOrder && tabOrder.length > 0
      ? tabOrder
      : Object.keys(conversations);
  const isActive = (name) => active === name;

  return (
    <Menu
      className="conversation-menu chat-tabs"
      size="small"
    >
      {names.map((name) => (
        <Menu.Item
          active={isActive(name)}
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={name}
          name={name}
          onClick={() => onConversationChange(name)}
        >
          <Icon
            color="green"
            name="circle"
            size="tiny"
          />
          {name}
          {conversations[name].hasUnAcknowledgedMessages && (
            <Label
              color="red"
              size="tiny"
            >
              {conversations[name].unAcknowledgedMessageCount}
            </Label>
          )}
        </Menu.Item>
      ))}
    </Menu>
  );
};

export default ChatMenu;
