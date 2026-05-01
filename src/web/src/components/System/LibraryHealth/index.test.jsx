import LibraryHealth from './index';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { addDiscoveryInboxItem } from '../../../lib/discoveryInbox';
import * as libraryHealth from '../../../lib/libraryHealth';
import * as searches from '../../../lib/searches';

vi.mock('../../../lib/libraryHealth', () => ({
  createRemediationJob: vi.fn(),
  getIssues: vi.fn(),
  getIssuesByArtist: vi.fn(),
  getIssuesByType: vi.fn(),
  getScanStatus: vi.fn(),
  getSummary: vi.fn(),
  startScan: vi.fn(),
  updateIssueStatus: vi.fn(),
}));

vi.mock('../../../lib/searches', () => ({
  createBatch: vi.fn(),
}));

vi.mock('../../../lib/discoveryInbox', () => ({
  addDiscoveryInboxItem: vi.fn(),
}));

vi.mock('semantic-ui-react', async () => {
  const actual = await vi.importActual('semantic-ui-react');
  const ReactModule = await import('react');
  const TestTab = ({ panes = [] }) =>
    ReactModule.default.createElement(
      'div',
      null,
      panes.map((pane) =>
        ReactModule.default.createElement(
          'div',
          { key: pane.menuItem.key },
          pane.render(),
        )),
    );
  TestTab.Pane = ({ children }) =>
    ReactModule.default.createElement('div', null, children);

  return {
    ...actual,
    Tab: TestTab,
  };
});

describe('LibraryHealth', () => {
  beforeEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
    libraryHealth.startScan.mockResolvedValue({ data: { scanId: 'scan-1' } });
    libraryHealth.getScanStatus.mockResolvedValue({ data: { status: 'Completed' } });
    libraryHealth.getSummary.mockResolvedValue({
      data: {
        issuesOpen: 1,
        issuesResolved: 2,
        totalIssues: 3,
      },
    });
    libraryHealth.getIssuesByType.mockResolvedValue({
      data: {
        groups: [{ count: 1, type: 'SuspectedTranscode' }],
      },
    });
    libraryHealth.getIssuesByArtist.mockResolvedValue({
      data: {
        groups: [{ artist: 'Fixture Artist', count: 1 }],
      },
    });
    libraryHealth.getIssues.mockResolvedValue({
      data: {
        issues: [
          {
            album: 'Fixture Album',
            artist: 'Fixture Artist',
            canAutoFix: true,
            issueId: 'issue-1',
            reason: 'Fixture reason',
            severity: 'High',
            status: 'Detected',
            title: 'Fixture Track',
            type: 'SuspectedTranscode',
          },
        ],
      },
    });
    searches.createBatch.mockResolvedValue(1);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('copies a read-only health report from loaded scan data', async () => {
    vi.useFakeTimers();
    render(<LibraryHealth />);

    fireEvent.change(screen.getByPlaceholderText('Enter library path (e.g., /music or C:\\Music)'), {
      target: { value: '/fixture/music' },
    });
    fireEvent.click(screen.getByText('Start Scan'));

    await vi.advanceTimersByTimeAsync(2_000);
    await waitFor(() => expect(libraryHealth.getSummary).toHaveBeenCalledWith('/fixture/music'));
    fireEvent.click(await screen.findByTestId('library-health-copy-report'));

    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Library health report prepared for 1 loaded issues.',
    );

    fireEvent.click(screen.getAllByRole('checkbox')[1]);
    fireEvent.click(screen.getByTestId('library-health-copy-action-plan'));

    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Library health action plan prepared for 1 selected issues.',
    );

    fireEvent.click(screen.getByTestId('library-health-copy-safe-fix-manifest'));

    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Library health safe-fix manifest prepared for 1 selected issues.',
    );

    fireEvent.click(screen.getByTestId('library-health-copy-search-seeds'));

    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Library health replacement search seeds prepared for 1 selected issues.',
    );

    fireEvent.click(screen.getByTestId('library-health-copy-quarantine-packet'));

    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Library health quarantine review packet prepared for 1 selected issues.',
    );
    expect(libraryHealth.createRemediationJob).not.toHaveBeenCalled();
  });

  it('runs bounded replacement searches and sends quarantine review candidates', async () => {
    vi.useFakeTimers();
    render(<LibraryHealth />);

    fireEvent.change(screen.getByPlaceholderText('Enter library path (e.g., /music or C:\\Music)'), {
      target: { value: '/fixture/music' },
    });
    fireEvent.click(screen.getByText('Start Scan'));

    await vi.advanceTimersByTimeAsync(2_000);
    await screen.findByText('Fixture Track');
    fireEvent.click(screen.getAllByRole('checkbox')[1]);
    fireEvent.click(screen.getByTestId('library-health-run-replacement-searches'));

    await waitFor(() => {
      expect(searches.createBatch).toHaveBeenCalledWith({
        queries: ['Fixture Artist Fixture Album Fixture Track'],
      });
    });
    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Started 1 bounded replacement search for selected Library Health issues.',
    );

    fireEvent.click(screen.getByTestId('library-health-send-quarantine-review'));

    expect(addDiscoveryInboxItem).toHaveBeenCalledWith(
      expect.objectContaining({
        evidenceKey: 'library-health:issue-1',
        source: 'Library Health',
      }),
    );
    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Sent 1 Library Health quarantine review candidate to Discovery Inbox.',
    );
  });

  it('queues remediation jobs only for selected auto-fixable issue ids', async () => {
    vi.useFakeTimers();
    render(<LibraryHealth />);

    fireEvent.change(screen.getByPlaceholderText('Enter library path (e.g., /music or C:\\Music)'), {
      target: { value: '/fixture/music' },
    });
    fireEvent.click(screen.getByText('Start Scan'));

    await vi.advanceTimersByTimeAsync(2_000);
    await screen.findByText('Fixture Track');
    fireEvent.click(screen.getAllByRole('checkbox')[1]);
    fireEvent.click(screen.getByText('Fix 1 Selected Issue'));

    await waitFor(() => {
      expect(libraryHealth.createRemediationJob).toHaveBeenCalledWith(['issue-1']);
    });
    expect(screen.getByTestId('library-health-report-message')).toHaveTextContent(
      'Queued remediation job for 1 auto-fixable issue.',
    );
  });
});
