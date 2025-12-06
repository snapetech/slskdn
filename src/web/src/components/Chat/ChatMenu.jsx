import './Chat.css';
import SendMessageModal from './SendMessageModal';
import React, { useState } from 'react';
import { Button, Icon, Input, Label, Menu } from 'semantic-ui-react';

const ChatMenu = ({
  active,
  conversations,
  onConversationChange,
  startConversation,
  ...rest
}) => {
  const [usernameInput, setUsernameInput] = useState('');
  const names = Object.keys(conversations);
  const isActive = (name) => active === name;

  const handleStartDirect = () => {
    if (usernameInput.trim()) {
      startConversation(usernameInput.trim());
      setUsernameInput('');
    }
  };

  return (
    <Menu
      className="conversation-menu"
      size="large"
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
      <Menu.Menu position="right">
        <Menu.Item>
          <Input
            action={{
              icon: 'chat',
              onClick: handleStartDirect,
            }}
            onChange={(event) => setUsernameInput(event.target.value)}
            onKeyUp={(event) => event.key === 'Enter' && handleStartDirect()}
            placeholder="Username..."
            size="small"
            value={usernameInput}
          />
        </Menu.Item>
        <SendMessageModal
          centered
          size="small"
          trigger={
            <Button
              className="add-button"
              icon
              title="Send message with text"
            >
              <Icon name="edit" />
            </Button>
          }
          {...rest}
        />
      </Menu.Menu>
    </Menu>
  );
};

export default ChatMenu;
