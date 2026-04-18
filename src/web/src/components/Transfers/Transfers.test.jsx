import '@testing-library/jest-dom';
import Transfers from './Transfers';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { toast } from 'react-toastify';
import { vi } from 'vitest';

import * as autoReplaceLibrary from '../../lib/autoReplace';
import * as transfersLibrary from '../../lib/transfers';

vi.mock('../../lib/autoReplace', () => ({
  getAutoReplaceStatus: vi.fn().mockResolvedValue({ enabled: false }),
  enableAutoReplace: vi.fn(),
  disableAutoReplace: vi.fn(),
}));

vi.mock('../../lib/transfers', () => ({
  getAll: vi.fn(),
  getPlaceInQueue: vi.fn(),
  download: vi.fn(),
  cancel: vi.fn(),
  clearCompleted: vi.fn(),
}));

vi.mock('../Shared', () => ({
  LoaderSegment: () => <div>Loading</div>,
  PlaceholderSegment: ({ caption }) => <div>{caption}</div>,
}));

vi.mock('./TransferGroup', () => ({
  default: () => <div>Transfer Group</div>,
}));

vi.mock('./TransfersHeader', () => ({
  default: ({ onRemoveAll, onRetryAll, transfers }) => {
    const files = transfers.flatMap((user) =>
      user.directories.flatMap((directory) => directory.files),
    );

    return (
      <div>
        <button onClick={() => onRetryAll(files)}>retry-all</button>
        <button onClick={() => onRetryAll(files)}>retry-all-again</button>
        <button onClick={() => onRemoveAll(files, false, { useBulkClear: true })}>
          remove-completed
        </button>
      </div>
    );
  },
}));

vi.mock('react-toastify', () => ({
  toast: {
    error: vi.fn(),
    info: vi.fn(),
  },
}));

const makeTransfers = (state = 'Completed, Errored') => [
  {
    username: 'alice',
    directories: [
      {
        directory: 'Album',
        files: [
          {
            direction: 'download',
            filename: 'one.mp3',
            id: '1',
            size: 1,
            state,
            username: 'alice',
          },
          {
            direction: 'download',
            filename: 'two.mp3',
            id: '2',
            size: 2,
            state,
            username: 'alice',
          },
        ],
      },
    ],
  },
];

describe('Transfers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    transfersLibrary.getAll.mockResolvedValue(makeTransfers());
    autoReplaceLibrary.getAutoReplaceStatus.mockResolvedValue({
      enabled: false,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('trickles bulk retries through the background queue one request at a time', async () => {
    const resolvers = [];

    transfersLibrary.download.mockImplementation(() => {
      return new Promise((resolve) => {
        resolvers.push(resolve);
      });
    });

    render(
      <Transfers
        direction="download"
        server={{ isConnected: true }}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'retry-all' }));

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(1);
    });

    resolvers.shift()({});

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(2);
    });

    resolvers.shift()({});

    await waitFor(() => {
      expect(toast.error).not.toHaveBeenCalled();
    });
  });

  it('dedupes bulk retries while the same work is already queued', async () => {
    const resolvers = [];

    transfersLibrary.download.mockImplementation(() => {
      return new Promise((resolve) => {
        resolvers.push(resolve);
      });
    });

    render(
      <Transfers
        direction="download"
        server={{ isConnected: true }}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'retry-all' }));
    fireEvent.click(screen.getByRole('button', { name: 'retry-all-again' }));

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(1);
    });

    resolvers.shift()({});

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(2);
    });

    resolvers.shift()({});

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(2);
    });
  });

  it('dedupes remove-all-completed while clear-completed is already queued', async () => {
    let resolveClearCompleted;
    transfersLibrary.getAll.mockResolvedValue(makeTransfers('Completed, Succeeded'));
    transfersLibrary.clearCompleted.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveClearCompleted = resolve;
        }),
    );

    render(
      <Transfers
        direction="download"
        server={{ isConnected: true }}
      />,
    );

    const button = await screen.findByRole('button', { name: 'remove-completed' });
    fireEvent.click(button);
    fireEvent.click(button);

    await waitFor(() => {
      expect(transfersLibrary.clearCompleted).toHaveBeenCalledTimes(1);
    });

    resolveClearCompleted({});

    await waitFor(() => {
      expect(transfersLibrary.clearCompleted).toHaveBeenCalledTimes(1);
    });
  });

  it('shows one bulk retry error instead of a toast per file', async () => {
    transfersLibrary.download.mockRejectedValue(new Error('boom'));

    render(
      <Transfers
        direction="download"
        server={{ isConnected: true }}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'retry-all' }));

    await waitFor(() => {
      expect(transfersLibrary.download).toHaveBeenCalledTimes(2);
    });
    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledTimes(1);
    });
    expect(toast.error).toHaveBeenCalledWith(
      expect.stringContaining('Failed to retry 2 transfer(s).'),
    );
  });
});
