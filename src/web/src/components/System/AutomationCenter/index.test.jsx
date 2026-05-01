// <copyright file="index.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import AutomationCenter from './index';
import {
  automationRecipeInputStorageKey,
  automationRecipeStorageKey,
  buildAutomationExecutionReport,
  buildAutomationDryRunReport,
  buildAutomationRunHistory,
  automationRecipes,
  formatAutomationRunHistoryReport,
} from '../../../lib/automationRecipes';
import * as libraryHealthAPI from '../../../lib/libraryHealth';
import * as wishlistAPI from '../../../lib/wishlist';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

vi.mock('react-toastify', () => ({
  toast: {
    info: vi.fn(),
  },
}));

vi.mock('../../../lib/wishlist', () => ({
  getAll: vi.fn(),
  runSearch: vi.fn(),
}));

vi.mock('../../../lib/libraryHealth', () => ({
  startScan: vi.fn(),
}));

describe('AutomationCenter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it('shows enabled and visible disabled automation recipes', () => {
    render(<AutomationCenter />);

    expect(screen.getByText('Automation Center')).toBeInTheDocument();
    expect(screen.getAllByText('Local Diagnostics').length).toBeGreaterThan(0);
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

    fireEvent.click(screen.getByRole('button', { name: 'Record dry run for Local Diagnostics' }));

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

  it('executes the Wishlist Retry automation through bounded backend searches', async () => {
    wishlistAPI.getAll.mockResolvedValue([
      {
        enabled: true,
        id: 'wishlist-request-1',
        searchText: 'Public Domain Suite',
      },
      {
        enabled: true,
        id: 'wishlist-request-2',
        searchText: 'Archive Concert Recording',
      },
      {
        enabled: false,
        id: 'wishlist-request-3',
        searchText: 'Disabled Request',
      },
    ]);
    wishlistAPI.runSearch.mockResolvedValue({ responseCount: 1 });
    render(<AutomationCenter />);

    fireEvent.click(screen.getByLabelText('Enable Wishlist Retry'));
    fireEvent.click(screen.getByRole('button', { name: 'Execute Wishlist Retry' }));

    await waitFor(() => {
      expect(wishlistAPI.runSearch).toHaveBeenCalledTimes(2);
    });
    expect(wishlistAPI.runSearch).toHaveBeenCalledWith('wishlist-request-1');
    expect(wishlistAPI.runSearch).toHaveBeenCalledWith('wishlist-request-2');
    expect(screen.getByText(/Wishlist Retry ran 2 bounded Wishlist searches/)).toBeInTheDocument();

    const stored = JSON.parse(localStorage.getItem(automationRecipeStorageKey));
    expect(stored['wishlist-retry'].lastRunReport).toEqual(
      expect.objectContaining({
        executed: true,
        failed: 0,
        recipeId: 'wishlist-retry',
        started: 2,
      }),
    );
  });

  it('executes Library Health scans with an explicit operator path', async () => {
    libraryHealthAPI.startScan.mockResolvedValue({
      data: {
        scanId: 'scan-public-domain',
      },
    });
    render(<AutomationCenter />);

    fireEvent.click(screen.getByLabelText('Enable Library Health Scan'));
    fireEvent.change(screen.getByLabelText('Library Health scan path'), {
      target: {
        value: '/fixture/library',
      },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Execute Library Health Scan' }));

    await waitFor(() => {
      expect(libraryHealthAPI.startScan).toHaveBeenCalledWith('/fixture/library');
    });
    expect(
      screen.getByText(/Library Health Scan started scan scan-public-domain/),
    ).toBeInTheDocument();

    const inputs = JSON.parse(localStorage.getItem(automationRecipeInputStorageKey));
    const stored = JSON.parse(localStorage.getItem(automationRecipeStorageKey));
    expect(inputs['library-health-scan'].libraryPath).toBe('/fixture/library');
    expect(stored['library-health-scan'].lastRunReport).toEqual(
      expect.objectContaining({
        executed: true,
        recipeId: 'library-health-scan',
        scanId: 'scan-public-domain',
        started: 1,
      }),
    );
  });

  it('copies automation review history without executing recipes', async () => {
    render(<AutomationCenter />);

    fireEvent.click(screen.getByRole('button', { name: 'Copy automation review history' }));

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        expect.stringContaining('slskdN automation review history'),
      );
    });
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

  it('builds explicit execution reports for live recipe runs', () => {
    expect(
      buildAutomationExecutionReport(
        automationRecipes.find((recipe) => recipe.id === 'wishlist-retry'),
        {
          failed: 1,
          runLimit: 3,
          skipped: 2,
          started: 2,
        },
        '2026-04-30T20:25:00.000Z',
      ),
    ).toEqual({
      approvalGate: 'Download approval',
      cooldown: '2 hours',
      executed: true,
      failed: 1,
      fileImpact: 'Downloads after approval',
      generatedAt: '2026-04-30T20:25:00.000Z',
      maxRunTime: '20 minutes',
      networkImpact: 'Public peers possible',
      recipeId: 'wishlist-retry',
      runLimit: 3,
      skipped: 2,
      started: 2,
      summary: 'Started 2 action(s); 1 failed; 2 skipped.',
      title: 'Wishlist Retry',
    });
  });

  it('builds copyable review history from enabled and dry-run recipes', () => {
    const state = {
      'local-diagnostics': {
        enabled: true,
        lastDryRunAt: '2026-04-30T20:20:00.000Z',
        lastDryRunReport: buildAutomationDryRunReport(
          automationRecipes.find((recipe) => recipe.id === 'local-diagnostics'),
          '2026-04-30T20:20:00.000Z',
        ),
      },
      'wishlist-retry': {
        enabled: false,
        lastRunAt: '2026-04-30T20:25:00.000Z',
        lastRunReport: buildAutomationExecutionReport(
          automationRecipes.find((recipe) => recipe.id === 'wishlist-retry'),
          {
            failed: 0,
            runLimit: 3,
            skipped: 0,
            started: 1,
            summary: 'Ran 1 Wishlist searches; 0 failed; 0 skipped.',
          },
          '2026-04-30T20:25:00.000Z',
        ),
      },
    };
    const history = buildAutomationRunHistory(state);
    const report = formatAutomationRunHistoryReport(history);

    expect(history).toHaveLength(2);
    expect(report).toContain('slskdN automation review history');
    expect(report).toContain('Local Diagnostics');
    expect(report).toContain('Executed: no');
    expect(report).toContain('Wishlist Retry');
    expect(report).toContain('Run summary: Ran 1 Wishlist searches; 0 failed; 0 skipped.');
  });
});
