import SetupHealthCheckModal from './SetupHealthCheckModal';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

describe('SetupHealthCheckModal', () => {
  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it('opens a setup health checklist from local state', () => {
    render(
      <SetupHealthCheckModal
        options={{
          directories: {
            downloads: '/fixture/downloads',
          },
          shares: {
            directories: ['/fixture/music'],
          },
        }}
        state={{
          connected: true,
          user: {
            username: 'fixture_user',
          },
        }}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Open setup health check' }));

    expect(screen.getByText('Ready')).toBeInTheDocument();
    expect(screen.getByText('Soulseek session')).toBeInTheDocument();
    expect(screen.getByText('Downloads')).toBeInTheDocument();
    expect(screen.getByText('Shares configured')).toBeInTheDocument();
  });

  it('copies the setup health report', async () => {
    render(
      <SetupHealthCheckModal
        state={{
          connected: false,
        }}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Open setup health check' }));
    fireEvent.click(screen.getByRole('button', { name: 'Copy setup health report' }));

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        expect.stringContaining('slskdN setup health check'),
      );
    });
  });
});
