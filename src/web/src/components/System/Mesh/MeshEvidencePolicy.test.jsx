import MeshEvidencePolicy from './MeshEvidencePolicy';
import { meshEvidencePolicyStorageKey } from '../../../lib/meshEvidencePolicy';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

describe('MeshEvidencePolicy', () => {
  beforeEach(() => {
    localStorage.clear();
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
});
