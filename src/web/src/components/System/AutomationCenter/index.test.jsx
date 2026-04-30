// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import AutomationCenter from './index';
import {
  automationRecipeStorageKey,
  buildAutomationDryRunReport,
  automationRecipes,
} from '../../../lib/automationRecipes';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

vi.mock('react-toastify', () => ({
  toast: {
    info: vi.fn(),
  },
}));

describe('AutomationCenter', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows enabled and visible disabled automation recipes', () => {
    render(<AutomationCenter />);

    expect(screen.getByText('Automation Center')).toBeInTheDocument();
    expect(screen.getByText('Local Diagnostics')).toBeInTheDocument();
    expect(screen.getByText('Wishlist Retry')).toBeInTheDocument();
    expect(screen.getByText('Visible Disabled')).toBeInTheDocument();
    expect(screen.getByText('Cooldown 2 hours')).toBeInTheDocument();
    expect(screen.getByText('Download approval')).toBeInTheDocument();
  });

  it('persists recipe enablement from the visible toggle', () => {
    render(<AutomationCenter />);

    fireEvent.click(screen.getByLabelText('Enable Wishlist Retry'));

    const stored = JSON.parse(localStorage.getItem(automationRecipeStorageKey));
    expect(stored['wishlist-retry'].enabled).toBe(true);
    expect(screen.getByLabelText('Disable Wishlist Retry')).toBeInTheDocument();
  });

  it('records dry-run checkpoints without executing the recipe', () => {
    render(<AutomationCenter />);

    fireEvent.click(screen.getAllByRole('button')[0]);

    const stored = JSON.parse(localStorage.getItem(automationRecipeStorageKey));
    expect(stored['local-diagnostics'].lastDryRunAt).toBeTruthy();
    expect(stored['local-diagnostics'].lastDryRunReport).toEqual(
      expect.objectContaining({
        executed: false,
        networkImpact: 'Local',
        recipeId: 'local-diagnostics',
      }),
    );
  });

  it('builds bounded dry-run reports without execution', () => {
    expect(
      buildAutomationDryRunReport(
        automationRecipes.find((recipe) => recipe.id === 'wishlist-retry'),
        '2026-04-30T20:20:00.000Z',
      ),
    ).toEqual({
      approvalGate: 'Download approval',
      cooldown: '2 hours',
      executed: false,
      fileImpact: 'Downloads after approval',
      generatedAt: '2026-04-30T20:20:00.000Z',
      maxRunTime: '20 minutes',
      networkImpact: 'Public peers possible',
      recipeId: 'wishlist-retry',
      title: 'Wishlist Retry',
    });
  });
});
