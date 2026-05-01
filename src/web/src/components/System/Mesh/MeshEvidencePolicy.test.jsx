import MeshEvidencePolicy from './MeshEvidencePolicy';
import { meshEvidencePolicyStorageKey } from '../../../lib/meshEvidencePolicy';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

describe('MeshEvidencePolicy', () => {
  beforeEach(() => {
    localStorage.clear();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it('renders private defaults for mesh evidence controls', () => {
    render(<MeshEvidencePolicy />);

    expect(screen.getByText('Mesh Evidence Policy')).toBeInTheDocument();
    expect(screen.getByText('Inbound Evidence')).toBeInTheDocument();
    expect(screen.getByText('Outbound Types')).toBeInTheDocument();
    expect(screen.getByText('Provenance')).toBeInTheDocument();
    expect(screen.getAllByText('Disabled').length).toBeGreaterThan(0);
    expect(screen.getByText('Hash verification')).toBeInTheDocument();
  });

  it('persists outbound evidence opt-in toggles', () => {
    render(<MeshEvidencePolicy />);

    fireEvent.click(
      screen.getByRole('checkbox', {
        name: 'Enable Hash verification publication',
      }),
    );

    const persisted = JSON.parse(
      localStorage.getItem(meshEvidencePolicyStorageKey),
    );

    expect(persisted.outbound.hashVerification).toBe(true);
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('resets policy to private defaults', () => {
    render(<MeshEvidencePolicy />);

    fireEvent.click(
      screen.getByRole('checkbox', {
        name: 'Enable Metadata corrections publication',
      }),
    );
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Reset mesh evidence policy to private defaults',
      }),
    );

    expect(localStorage.getItem(meshEvidencePolicyStorageKey)).toBeNull();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('reviews pasted mesh evidence locally and copies the report', async () => {
    render(<MeshEvidencePolicy />);

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Load sample mesh evidence',
      }),
    );
    fireEvent.click(
      screen.getByRole('listbox', {
        name: 'Mesh evidence inbound trust tier',
      }),
    );
    fireEvent.click(screen.getByRole('option', { name: 'Trusted realms' }));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Review mesh evidence locally',
      }),
    );

    expect(screen.getByText('Evidence Review Sandbox')).toBeInTheDocument();
    expect(screen.getByText('Accepted')).toBeInTheDocument();
    expect(screen.getByText('Rejected')).toBeInTheDocument();
    expect(screen.getByText(/contains raw path data/)).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Copy mesh evidence review report',
      }),
    );

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      expect.stringContaining('slskdN mesh evidence review'),
    );
  });
});
