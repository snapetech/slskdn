import '@testing-library/jest-dom';
import * as chat from '../../lib/chat';
import Messaging from './Messaging';
import React from 'react';
import * as rooms from '../../lib/rooms';
import { MemoryRouter } from 'react-router-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/chat', () => ({
  getAll: vi.fn(),
  remove: vi.fn(),
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

describe('Messaging', () => {
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
});
