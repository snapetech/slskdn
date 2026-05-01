import {
  buildMetadataMatch,
  getVersionTags,
  normalizeMetadataText,
  scoreMetadataCandidate,
} from './metadataMatcher';

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
        band: 'Auto',
        confidence: 0.98,
        strongestEvidence: 'Parsed artist, album, and title from separated filename parts.',
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

  it('normalizes unicode, punctuation, symbols, and case for reusable matching', () => {
    expect(normalizeMetadataText('Beyoncé & Jay-Z - Déjà Vu!!!')).toBe(
      'beyonce and jay z deja vu',
    );
    expect(getVersionTags('Song (Live Acoustic Remaster)')).toEqual([
      'acoustic',
      'live',
      'remaster',
    ]);
  });

  it('scores metadata candidates with identifier, duration, and explanation evidence', () => {
    const match = scoreMetadataCandidate(
      {
        artist: 'Beyonce',
        durationMs: 241_000,
        isrc: 'US123',
        title: 'Deja Vu',
      },
      {
        artist: 'Beyoncé',
        durationMs: 241_500,
        isrc: 'US123',
        title: 'Déjà Vu',
      },
    );

    expect(match).toEqual(
      expect.objectContaining({
        band: 'Auto',
        confidence: expect.any(Number),
        versionMismatch: false,
      }),
    );
    expect(match.confidence).toBeGreaterThanOrEqual(0.9);
    expect(match.strongestEvidence).toMatch(/external identifier/i);
  });

  it('protects short titles without exact duration or identifier evidence', () => {
    const match = scoreMetadataCandidate(
      {
        artist: 'Can',
        durationMs: 180_000,
        title: 'Oh',
      },
      {
        artist: 'Can',
        durationMs: 210_000,
        title: 'Oh',
      },
    );

    expect(match.band).toBe('Reject');
    expect(match.warnings).toContain(
      'Short title requires exact duration or identifier evidence.',
    );
  });

  it('sends version mismatches to review or reject with weak evidence', () => {
    const match = scoreMetadataCandidate(
      {
        artist: 'Artist',
        durationMs: 200_000,
        title: 'Song',
      },
      {
        artist: 'Artist',
        durationMs: 200_000,
        title: 'Song Live Remix',
      },
    );

    expect(match.versionMismatch).toBe(true);
    expect(match.weakestEvidence).toBe(
      'Version tags differ between expected metadata and candidate.',
    );
  });
});
