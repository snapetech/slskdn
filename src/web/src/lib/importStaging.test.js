import {
  addImportStagingFiles,
  getImportStagingItems,
  importStagingStorageKey,
  matchAllImportStagingItems,
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
        lastModified: 123,
        size: 3,
        state: 'Staged',
        type: 'audio/flac',
      }),
    );
    expect(JSON.parse(localStorage.getItem(importStagingStorageKey))).toHaveLength(1);
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
});
