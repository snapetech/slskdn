import * as rooms from '../../lib/rooms';
import React, { Component, createRef } from 'react';
import {
  Button,
  Card,
  Dimmer,
  Icon,
  Input,
  List,
  Loader,
  Portal,
  Ref,
  Segment,
} from 'semantic-ui-react';

const initialState = {
  contextMenu: {
    message: null,
    open: false,
    x: 0,
    y: 0,
  },
  loading: false,
  room: {
    messages: [],
    users: [],
  },
};

class RoomSession extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
    this.listRef = createRef();
    this.messageRef = undefined;
  }

  componentDidMount() {
    this.fetchRoom();
    this.interval = window.setInterval(this.fetchRoom, 1_000);
    document.addEventListener('click', this.handleCloseContextMenu);
  }

  componentWillUnmount() {
    clearInterval(this.interval);
    document.removeEventListener('click', this.handleCloseContextMenu);
  }

  componentDidUpdate(previousProps) {
    if (previousProps.roomName !== this.props.roomName) {
      this.setState(initialState, () => {
        this.fetchRoom();
      });
    }
  }

  fetchRoom = async () => {
    const { roomName } = this.props;

    if (!roomName || roomName.length === 0) return;

    try {
      const messages = await rooms.getMessages({ roomName });
      const users = await rooms.getUsers({ roomName });

      this.setState({
        loading: false,
        room: {
          messages,
          users,
        },
      });
    } catch (error) {
      console.error('Failed to fetch room data:', error);
      this.setState({ loading: false });
    }
  };

  validInput = () =>
    (this.props.roomName || '').length > 0 &&
    (
      (this.messageRef &&
        this.messageRef.current &&
        this.messageRef.current.value) ||
      ''
    ).length > 0;

  focusInput = () => {
    if (this.messageRef && this.messageRef.current) {
      this.messageRef.current.focus();
    }
  };

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    const dtfUS = new Intl.DateTimeFormat('en', {
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      month: 'numeric',
    });

    return dtfUS.format(date);
  };

  sendMessage = async () => {
    const { roomName } = this.props;
    const message = this.messageRef.current.value;

    if (!this.validInput()) {
      return;
    }

    try {
      await rooms.sendMessage({ message, roomName });
      this.messageRef.current.value = '';
    } catch (error) {
      console.error('Failed to send message:', error);
    }
  };

  handleContextMenu = (clickEvent, message) => {
    clickEvent.preventDefault();
    this.setState({
      contextMenu: {
        message,
        open: true,
        x: clickEvent.pageX,
        y: clickEvent.pageY,
      },
    });
  };

  handleCloseContextMenu = () => {
    this.setState((previousState) => ({
      contextMenu: {
        ...previousState.contextMenu,
        open: false,
      },
    }));
  };

  handleReply = () => {
    if (this.messageRef && this.messageRef.current) {
      this.messageRef.current.value = `[${this.state.contextMenu.message.username}] ${this.state.contextMenu.message.message} --> `;
      this.focusInput();
    }
  };

  handleUserProfile = () => {
    if (this.props.onUserProfile) {
      this.props.onUserProfile(this.state.contextMenu.message.username);
    }
  };

  handleBrowseShares = () => {
    if (this.props.onBrowseShares) {
      this.props.onBrowseShares(this.state.contextMenu.message.username);
    }
  };

  renderContextMenu() {
    const { contextMenu } = this.state;
    return (
      <Portal open={contextMenu.open}>
        <div
          className="ui vertical buttons popup-menu"
          style={{
            left: contextMenu.x,
            maxHeight: `calc(100vh - ${contextMenu.y}px)`,
            top: contextMenu.y,
          }}
        >
          <Button
            className="ui compact button popup-option"
            onClick={this.handleReply}
          >
            Reply
          </Button>
          <Button
            className="ui compact button popup-option"
            onClick={this.handleUserProfile}
          >
            User Profile
          </Button>
          <Button
            className="ui compact button popup-option"
            onClick={this.handleBrowseShares}
          >
            Browse Shares
          </Button>
        </div>
      </Portal>
    );
  }

  render() {
    const { onLeaveRoom, roomName } = this.props;

    const { contextMenu, loading, room } = this.state;

    if (!roomName || roomName.length === 0) {
      return (
        <div className="room-session-empty">
          <Segment placeholder>
            <Icon
              name="comments"
              size="big"
            />
            <p>Select a room to start chatting</p>
          </Segment>
        </div>
      );
    }

    return (
      <div className="room-session">
        <Card
          className="room-active-card"
          raised
        >
          <Card.Content onClick={() => this.focusInput()}>
            <Card.Header>
              <Icon
                color="green"
                name="circle"
              />
              {roomName}
              <Icon
                className="close-button"
                color="red"
                link
                name="close"
                onClick={() => onLeaveRoom && onLeaveRoom(roomName)}
              />
            </Card.Header>
            <div className="room">
              {loading ? (
                <Dimmer
                  active
                  inverted
                >
                  <Loader inverted />
                </Dimmer>
              ) : (
                <>
                  <Segment.Group>
                    <Segment className="room-history">
                      <Ref innerRef={this.listRef}>
                        <List>
                          {room.messages.map((message) => (
                            <div
                              key={`${message.timestamp}+${message.message}`}
                              onContextMenu={(clickEvent) =>
                                this.handleContextMenu(clickEvent, message)
                              }
                            >
                              <List.Content
                                className={`room-message ${message.self ? 'room-message-self' : ''}`}
                              >
                                <span className="room-message-time">
                                  {this.formatTimestamp(message.timestamp)}
                                </span>
                                <span className="room-message-name">
                                  {message.username}:{' '}
                                </span>
                                <span className="room-message-message">
                                  {message.message}
                                </span>
                              </List.Content>
                            </div>
                          ))}
                          <List.Content id="room-history-scroll-anchor" />
                        </List>
                      </Ref>
                    </Segment>
                    <Segment className="room-input">
                      <Input
                        action={{
                          className: 'room-message-button',
                          disabled: !this.validInput(),
                          icon: (
                            <Icon
                              color="green"
                              name="send"
                            />
                          ),
                          onClick: this.sendMessage,
                        }}
                        fluid
                        input={
                          <input
                            autoComplete="off"
                            data-lpignore="true"
                            id="room-message-input"
                            type="text"
                          />
                        }
                        onKeyUp={(event) =>
                          event.key === 'Enter' ? this.sendMessage() : ''
                        }
                        ref={(input) =>
                          (this.messageRef = input && input.inputRef)
                        }
                        transparent
                      />
                    </Segment>
                  </Segment.Group>
                  <Segment className="room-users">
                    <div className="room-users-header">
                      <Icon name="users" />
                      Users ({room.users.length})
                    </div>
                    <List
                      divided
                      relaxed
                    >
                      {room.users.map((user) => (
                        <List.Item key={user.username}>
                          <List.Content>
                            <List.Header>{user.username}</List.Header>
                            <List.Description>
                              {user.status === 1 ? 'Away' : 'Online'}
                            </List.Description>
                          </List.Content>
                        </List.Item>
                      ))}
                    </List>
                  </Segment>
                </>
              )}
            </div>
          </Card.Content>
        </Card>
        {this.renderContextMenu()}
      </div>
    );
  }
}

export default RoomSession;
