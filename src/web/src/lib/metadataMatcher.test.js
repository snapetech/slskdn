import { buildMetadataMatch } from './metadataMatcher';

describe('metadataMatcher', () => {
  it('builds a strong match from artist album track filenames', () => {
    const match = buildMetadataMatch({
      fileName: 'The Artist - The Album - 03 - The Song.flac',
      type: 'audio/flac',
    });

    expect(match).toEqual(
      expect.objectContaining({
        album: 'The Album',
        artist: 'The Artist',
        confidence: 0.98,
        status: 'Strong Match',
        title: 'The Song',
        trackNumber: 3,
      }),
    );
    expect(match.warnings).toEqual([]);
  });

  it('flags low-confidence filenames for review', () => {
    const match = buildMetadataMatch({
      fileName: 'unknown.bin',
      type: 'application/octet-stream',
    });

    expect(match.status).toBe('Needs Review');
    expect(match.confidence).toBeLessThan(0.75);
    expect(match.warnings).toContain(
      'File extension or MIME type is not a known audio format.',
    );
    expect(match.warnings).toContain('Artist could not be inferred confidently.');
  });
});
