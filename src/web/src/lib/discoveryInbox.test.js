import {
  addDiscoveryInboxItem,
  discoveryInboxStorageKey,
  getDiscoveryInboxItems,
  getDiscoveryInboxSnoozeStatus,
  snoozeDiscoveryInboxItem,
  updateDiscoveryInboxItemState,
} from './discoveryInbox';

describe('discoveryInbox', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('stores snooze due dates without losing saved evidence', () => {
    const item = addDiscoveryInboxItem({
      evidenceKey: 'manual-search:snooze',
      reason: 'Manual review.',
      searchText: 'snooze',
      source: 'Search',
    });

    snoozeDiscoveryInboxItem(item.id, 7, {
      timestamp: Date.parse('2026-04-30T00:00:00.000Z'),
    });

    const [persisted] = JSON.parse(
      localStorage.getItem(discoveryInboxStorageKey),
    );

    expect(persisted).toMatchObject({
      evidenceKey: 'manual-search:snooze',
      snoozedUntil: '2026-05-07T00:00:00.000Z',
      state: 'Snoozed',
    });
  });

  it('clears snooze due date when the item returns to review', () => {
    const item = addDiscoveryInboxItem({
      searchText: 'return me',
    });

    snoozeDiscoveryInboxItem(item.id, 7, {
      timestamp: Date.parse('2026-04-30T00:00:00.000Z'),
    });
    updateDiscoveryInboxItemState(item.id, 'Suggested');

    expect(getDiscoveryInboxItems()[0]).toMatchObject({
      snoozedUntil: '',
      state: 'Suggested',
    });
  });

  it('marks overdue snoozes as due', () => {
    expect(
      getDiscoveryInboxSnoozeStatus(
        {
          snoozedUntil: '2026-04-29T00:00:00.000Z',
          state: 'Snoozed',
        },
        Date.parse('2026-04-30T00:00:00.000Z'),
      ),
    ).toMatchObject({
      isDue: true,
      label: 'Snooze due',
    });
  });
});
