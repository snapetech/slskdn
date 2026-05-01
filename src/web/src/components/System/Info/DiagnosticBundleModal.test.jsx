import DiagnosticBundleModal from './DiagnosticBundleModal';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

describe('DiagnosticBundleModal', () => {
  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it('opens a redacted diagnostic bundle', () => {
    render(
      <DiagnosticBundleModal
        options={{
          directories: {
            downloads: '/fixture/downloads',
          },
          integration: {
            apiKey: 'secret-key',
            label: 'fixture-provider',
          },
          shares: {
            directories: ['/fixture/music'],
          },
          web: {
            authentication: {
              apiKey: 'secret-api-key',
            },
          },
        }}
        state={{
          connected: true,
          sessionToken: 'secret-token',
          user: {
            username: 'fixture_user',
          },
        }}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Open diagnostic bundle' }));

    expect(screen.getByText('Redacted support snapshot')).toBeInTheDocument();
    const bundle = screen.getByLabelText('Redacted diagnostic bundle');
    expect(bundle.value).toContain('label: fixture-provider');
    expect(bundle.value).toContain('setupHealth:');
    expect(bundle.value).toContain('readiness: Ready');
    expect(bundle.value).toContain('apiKey: "[redacted]"');
    expect(bundle.value).toContain('sessionToken: "[redacted]"');
    expect(bundle.value).not.toContain('secret-key');
    expect(bundle.value).not.toContain('secret-api-key');
    expect(bundle.value).not.toContain('secret-token');
  });

  it('copies the redacted diagnostic bundle', async () => {
    render(
      <DiagnosticBundleModal
        state={{
          connected: true,
        }}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Open diagnostic bundle' }));
    fireEvent.click(screen.getByRole('button', { name: 'Copy diagnostic bundle' }));

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        expect.stringContaining('connected: true'),
      );
    });
  });
});
