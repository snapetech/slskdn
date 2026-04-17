import { activeChatKey } from '../../config';
import UserNoteModal from '../Users/UserNoteModal';
import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Dropdown } from 'semantic-ui-react';

const UserContextMenu = ({ children, trigger, username }) => {
  const navigate = useNavigate();

  const handleBrowse = () => {
    navigate('/browse', { state: { user: username } });
  };

  const handleChat = () => {
    sessionStorage.setItem(activeChatKey, username);
    navigate('/chat');
  };

  return (
    <Dropdown
      className="user-context-menu"
      pointing="left"
      trigger={trigger || children}
    >
      <Dropdown.Menu>
        <Dropdown.Header
          content={username}
          icon="user"
        />
        <Dropdown.Item
          icon="folder open"
          onClick={handleBrowse}
          text="Browse Files"
        />
        <Dropdown.Item
          icon="comments"
          onClick={handleChat}
          text="Private Chat"
        />
        <UserNoteModal
          trigger={
            <Dropdown.Item
              icon="sticky note"
              text="User Notes"
            />
          }
          username={username}
        />
      </Dropdown.Menu>
    </Dropdown>
  );
};

export default UserContextMenu;
