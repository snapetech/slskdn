import '@testing-library/jest-dom';
import ExperienceSettings from './index';
import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const storageKey = 'slskdn:experience-preferences:v1';

describe('ExperienceSettings', () => {
  beforeEach(() => {
    localStorage.clear();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it('surfaces Search, Player, and Messages preferences', () => {
    render(<ExperienceSettings />);

    expect(screen.getByText('Search')).toBeInTheDocument();
    expect(screen.queryByText('Discovery Inbox')).not.toBeInTheDocument();
    expect(screen.getByText('Player')).toBeInTheDocument();
    expect(screen.getByText('Messages')).toBeInTheDocument();
    expect(
      screen.getByLabelText('Enable search duplicate folding preference'),
    ).toBeChecked();
    expect(screen.getByLabelText('Show unread message badges preference')).toBeChecked();
  });

  it('saves local preferences without a backend call', () => {
    render(<ExperienceSettings />);

    fireEvent.click(screen.getByLabelText('Enable search duplicate folding preference'));
    fireEvent.click(screen.getByLabelText('Enable player queue auto-fill preference'));
    fireEvent.click(screen.getByRole('button', { name: 'Save Local Preferences' }));

    const saved = JSON.parse(localStorage.getItem(storageKey));
    expect(saved.searchDuplicateFolding).toBe(false);
    expect(saved.playerQueueAutoFill).toBe(true);
    expect(
      screen.getByText('Experience preferences saved locally in this browser.'),
    ).toBeInTheDocument();
  });

  it('copies a preference report', () => {
    render(<ExperienceSettings />);

    fireEvent.click(screen.getByRole('button', { name: 'Copy Report' }));

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      expect.stringContaining('slskdN experience preferences'),
    );
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      expect.stringContaining('Messages:'),
    );
  });
});
