import {
  addImportStagingFiles,
  addImportStagingItemToDenylist,
  applyAudioVerificationPolicy,
  getImportStagingDenylist,
  getImportStagingItems,
  importStagingDenylistStorageKey,
  importStagingStorageKey,
  removeImportStagingDenylistEntry,
  matchAllImportStagingItems,
  overrideImportStagingItemMetadataMatch,
  updateImportStagingItemAudioVerification,
  updateImportStagingItemMetadataMatch,
  updateImportStagingItemState,
} from './importStaging';

describe('importStaging', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('adds unique file metadata to local staging', () => {
    const file = new File(['abc'], 'track.flac', {
      lastModified: 123,
      type: 'audio/flac',
    });

    const items = addImportStagingFiles([file, file]);

    expect(items).toHaveLength(1);
    expect(items[0]).toEqual(
      expect.objectContaining({
        fileName: 'track.flac',
        audioVerification: null,
        fingerprintVerification: null,
        lastModified: 123,
        size: 3,
        state: 'Staged',
        type: 'audio/flac',
      }),
    );
    expect(JSON.parse(localStorage.getItem(importStagingStorageKey))).toHaveLength(1);
  });

  it('stores audio verification decisions with staged files', () => {
    const items = addImportStagingFiles([
      {
        audioVerification: {
          action: 'Review',
          confidence: 0.7,
          profileId: 'balanced',
          status: 'Review',
        },
        lastModified: 123,
        name: 'verified.flac',
        size: 3,
        type: 'audio/flac',
      },
    ]);

    expect(items[0].audioVerification).toEqual(
      expect.objectContaining({
        action: 'Review',
        profileId: 'balanced',
      }),
    );
  });

  it('updates staged audio verification and maps policy actions to review states', () => {
    addImportStagingFiles([
      new File(['abc'], 'track.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
    ]);
    const [item] = getImportStagingItems();

    const items = updateImportStagingItemAudioVerification(
      item.id,
      {
        action: 'Quarantine',
        confidence: 0.2,
        profileId: 'lossless-exact',
        status: 'Failed',
      },
      {
        fingerprintVerification: {
          algorithm: 'sha256',
          status: 'Verified',
          value: 'abc123',
        },
      },
    );

    expect(items[0]).toEqual(
      expect.objectContaining({
        audioVerification: expect.objectContaining({
          action: 'Quarantine',
          profileId: 'lossless-exact',
        }),
        fingerprintVerification: expect.objectContaining({
          value: 'abc123',
        }),
      }),
    );
    expect(applyAudioVerificationPolicy(items[0])).toBe('Failed');
  });

  it('updates staged item state', () => {
    addImportStagingFiles([
      new File(['abc'], 'track.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
    ]);
    const [item] = getImportStagingItems();

    const items = updateImportStagingItemState(item.id, 'Ready');

    expect(items[0].state).toBe('Ready');
  });

  it('stores metadata match results for one or all staged items', () => {
    addImportStagingFiles([
      new File(['abc'], 'Artist - Album - 01 - Track.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
      new File(['abc'], 'Mystery.bin', {
        lastModified: 456,
        type: 'application/octet-stream',
      }),
    ]);
    const first = getImportStagingItems().find((item) =>
      item.fileName.startsWith('Artist'),
    );

    let items = updateImportStagingItemMetadataMatch(first.id);
    expect(items.find((item) => item.id === first.id).metadataMatch).toEqual(
      expect.objectContaining({
        artist: 'Artist',
        status: 'Strong Match',
      }),
    );

    items = matchAllImportStagingItems();
    expect(items.every((item) => item.metadataMatch)).toBe(true);
  });

  it('stores manual metadata overrides as accepted matches', () => {
    addImportStagingFiles([
      new File(['abc'], 'mystery.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
    ]);
    const [item] = getImportStagingItems();

    const items = overrideImportStagingItemMetadataMatch(item.id, {
      artist: 'Manual Artist',
      title: 'Manual Title',
    });

    expect(items[0]).toEqual(
      expect.objectContaining({
        metadataOverride: expect.objectContaining({
          artist: 'Manual Artist',
          title: 'Manual Title',
        }),
        metadataMatch: expect.objectContaining({
          band: 'Auto',
          confidence: 1,
          status: 'Manual Override',
        }),
      }),
    );
  });

  it('blocks re-added files from the failed-import denylist', () => {
    addImportStagingFiles([
      new File(['abc'], 'bad.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
    ]);
    const [item] = getImportStagingItems();

    let denylist = addImportStagingItemToDenylist(
      item.id,
      'Bad import candidate.',
    );
    expect(denylist).toHaveLength(1);
    expect(JSON.parse(localStorage.getItem(importStagingDenylistStorageKey))).toHaveLength(1);

    localStorage.setItem(importStagingStorageKey, '[]');
    const items = addImportStagingFiles([
      new File(['abc'], 'bad.flac', {
        lastModified: 123,
        type: 'audio/flac',
      }),
    ]);

    expect(items[0]).toEqual(
      expect.objectContaining({
        fileName: 'bad.flac',
        reason: 'Blocked by failed-import denylist.',
        state: 'Failed',
      }),
    );

    denylist = removeImportStagingDenylistEntry(denylist[0].key);
    expect(denylist).toHaveLength(0);
    expect(getImportStagingDenylist()).toHaveLength(0);
  });
});
