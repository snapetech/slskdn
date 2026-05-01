import '@testing-library/jest-dom';
import * as chat from '../../lib/chat';
import Messaging from './Messaging';
import * as pods from '../../lib/pods';
import React from 'react';
import * as rooms from '../../lib/rooms';
import { MemoryRouter } from 'react-router-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/chat', () => ({
  getAll: vi.fn(),
  remove: vi.fn(),
  sendBatch: vi.fn(),
}));

vi.mock('../../lib/pods', () => ({
  get: vi.fn(),
  getMembers: vi.fn(),
  getMessages: vi.fn(),
  leave: vi.fn(),
  list: vi.fn(),
  sendMessage: vi.fn(),
}));

vi.mock('../../lib/rooms', () => ({
  getAvailable: vi.fn(),
  getJoined: vi.fn(),
  join: vi.fn(),
  leave: vi.fn(),
}));

vi.mock('../Chat/ChatSession', () => ({
  default: ({ username }) => <div>Chat panel: {username}</div>,
}));

vi.mock('../Rooms/RoomCreateModal', () => ({
  default: () => <button type="button">Create Room</button>,
}));

vi.mock('../Rooms/RoomSession', () => ({
  default: ({ roomName }) => <div>Room panel: {roomName}</div>,
}));

vi.mock('../Player/PodListenAlongPanel', () => ({
  default: ({ channelId, compact }) => (
    <div>
      Listen Along {channelId} {compact ? 'compact' : 'full'}
    </div>
  ),
}));

describe('Messaging', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.clearAllMocks();
    pods.getMembers.mockResolvedValue([]);
    pods.getMessages.mockResolvedValue([]);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  it('opens chat and room panels and collapses them into the dock', async () => {
    chat.getAll.mockResolvedValue([
      {
        hasUnAcknowledgedMessages: true,
        unAcknowledgedMessageCount: 2,
        username: 'friend',
      },
    ]);
    rooms.getJoined.mockResolvedValue(['indie']);
    rooms.getAvailable.mockResolvedValue([
      {
        name: 'ambient',
        userCount: 9,
      },
    ]);
    pods.list.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Saved Chats')).toBeInTheDocument();
    fireEvent.click(screen.getByText('friend'));
    fireEvent.click(screen.getByText('#indie'));

    expect(screen.getByText('Chat panel: friend')).toBeInTheDocument();
    expect(screen.getByText('Room panel: indie')).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Collapse #indie'));

    await waitFor(() => {
      expect(screen.queryByText('Room panel: indie')).not.toBeInTheDocument();
    });
    expect(screen.getAllByText('#indie').length).toBeGreaterThan(0);
  });

  it('starts a direct-message panel from the username input', async () => {
    chat.getAll.mockResolvedValue([]);
    pods.list.mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <Messaging />
      </MemoryRouter>,
    );

    fireEvent.change(await screen.findByLabelText('Chat username'), {
      target: { value: 'new-user' },
    });
    fireEvent.click(screen.getByLabelText('Open direct-message panel'));

    expect(screen.getByText('Chat panel: new-user')).toBeInTheDocument();
  });

  it('sends one batch private message to multiple recipients', async () => {
    chat.getAll.mockResolvedValue([]);
    chat.sendBatch.mockResolvedValue({});
    pods.list.mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <Messaging />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Workspace')).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Open batch private-message dialog'));
    fireEvent.change(screen.getByLabelText('Batch private-message recipients'), {
      target: { value: 'alice, bob' },
    });
    fireEvent.change(screen.getByLabelText('Batch private-message body'), {
      target: { value: 'hello' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() =>
      expect(chat.sendBatch).toHaveBeenCalledWith({
        message: 'hello',
        usernames: ['alice', 'bob'],
      }),
    );
  });

  it('hides pod direct channels from the unified message workspace', async () => {
    chat.getAll.mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);
    pods.list.mockResolvedValue([
      {
        channels: [{ channelId: 'dm', kind: 'Direct', name: 'dm' }],
        name: 'hunterbiden5000',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [{ channelId: 'dm', kind: 'Direct', name: 'dm' }],
      name: 'hunterbiden5000',
      podId: 'pod-1',
    });
    pods.getMessages.mockResolvedValue([
      {
        body: 'hello from pod',
        senderPeerId: 'friend',
        timestampUnixMs: Date.now(),
      },
    ]);

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Pod Channels')).toBeInTheDocument();
    expect(screen.queryByText('hunterbiden5000 / dm')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Message hunterbiden5000 / dm')).not.toBeInTheDocument();
    expect(screen.queryByText(/Listen Along dm/)).not.toBeInTheDocument();
  });

  it('folds pod direct channels into matching saved chats', async () => {
    chat.getAll.mockResolvedValue([{ username: 'hunterbiden5000' }]);
    rooms.getJoined.mockResolvedValue([]);
    pods.list.mockResolvedValue([
      {
        channels: [
          { channelId: 'dm', kind: 'Direct', name: 'dm' },
          { channelId: 'general', kind: 'Room', name: 'General' },
        ],
        name: 'hunterbiden5000',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [
        { channelId: 'dm', kind: 'Direct', name: 'dm' },
        { channelId: 'general', kind: 'Room', name: 'General' },
      ],
      name: 'hunterbiden5000',
      podId: 'pod-1',
    });

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('hunterbiden5000')).toBeInTheDocument();
    expect(screen.getByText('pod')).toBeInTheDocument();
    expect(screen.queryByText('hunterbiden5000 / dm')).not.toBeInTheDocument();
    expect(screen.getByText('hunterbiden5000 / General')).toBeInTheDocument();
  });

  it('does not reveal a pod direct channel after deleting a matching saved chat', async () => {
    chat.getAll
      .mockResolvedValueOnce([{ username: 'hunterbiden5000' }])
      .mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);
    pods.list.mockResolvedValue([
      {
        channels: [{ channelId: 'dm', kind: 'Direct', name: 'dm' }],
        name: 'hunterbiden5000',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [{ channelId: 'dm', kind: 'Direct', name: 'dm' }],
      name: 'hunterbiden5000',
      podId: 'pod-1',
    });

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('hunterbiden5000')).toBeInTheDocument();
    expect(screen.queryByText('hunterbiden5000 / dm')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Delete message thread with hunterbiden5000'));
    await waitFor(() => {
      expect(chat.remove).toHaveBeenCalledWith({ username: 'hunterbiden5000' });
    });

    expect(screen.queryByText('hunterbiden5000 / dm')).not.toBeInTheDocument();
  });

  it('shows compact listen-along only for pod room channels', async () => {
    chat.getAll.mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);
    pods.list.mockResolvedValue([
      {
        channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
        name: 'Gold Star Club',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
      name: 'Gold Star Club',
      podId: 'pod-1',
    });
    pods.getMessages.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByText('Gold Star Club / General'));

    expect(await screen.findByText('Listen Along general compact')).toBeInTheDocument();
  });

  it('uses the room-style member rail for pod room channels', async () => {
    chat.getAll.mockResolvedValue([]);
    rooms.getJoined.mockResolvedValue([]);
    pods.list.mockResolvedValue([
      {
        channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
        name: 'Gold Star Club',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
      name: 'Gold Star Club',
      podId: 'pod-1',
    });
    pods.getMembers.mockResolvedValue([{ peerId: 'member-one', role: 'Member' }]);
    pods.getMessages.mockResolvedValue([
      {
        body: 'pod room message',
        senderPeerId: 'member-one',
        timestampUnixMs: Date.now(),
      },
    ]);

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByText('Gold Star Club / General'));

    expect(await screen.findByText('Members (1)')).toBeInTheDocument();
    expect(screen.getByText('pod room message')).toBeInTheDocument();
    expect(screen.getByLabelText('Message Gold Star Club / General')).toBeInTheDocument();
  });

  it('exposes confirmed destructive actions for chats, rooms, and pods', async () => {
    chat.getAll.mockResolvedValue([{ username: 'friend' }]);
    rooms.getJoined.mockResolvedValue(['indie']);
    pods.list.mockResolvedValue([
      {
        channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
        name: 'Gold Star Club',
        podId: 'pod-1',
      },
    ]);
    pods.get.mockResolvedValue({
      channels: [{ channelId: 'general', kind: 'Room', name: 'General' }],
      name: 'Gold Star Club',
      podId: 'pod-1',
    });

    render(
      <MemoryRouter>
        <Messaging state={{ user: { username: 'me' } }} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('friend')).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Delete message thread with friend'));
    expect(chat.remove).toHaveBeenCalledWith({ username: 'friend' });

    fireEvent.click(screen.getByLabelText('Leave room indie'));
    expect(rooms.leave).toHaveBeenCalledWith({ roomName: 'indie' });

    fireEvent.click(screen.getByLabelText('Leave pod Gold Star Club'));
    expect(pods.leave).toHaveBeenCalledWith('pod-1', 'me');
  });
});
