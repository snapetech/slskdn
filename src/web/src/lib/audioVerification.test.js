import {
  audioVerificationCacheStorageKey,
  buildAudioVerificationDecision,
  clearAudioVerificationCache,
  getAudioVerificationCache,
  verifyAudioFile,
} from './audioVerification';

describe('audioVerification', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('passes strict verification with matching hash and sane lossless hints', () => {
    const result = buildAudioVerificationDecision({
      expected: {
        durationMs: 180_000,
        sha256: 'abc123',
      },
      file: {
        durationMs: 181_000,
        fileName: 'track.flac',
        size: 20_000_000,
        type: 'audio/flac',
      },
      fingerprint: {
        status: 'Verified',
        value: 'abc123',
      },
      profileId: 'lossless-exact',
    });

    expect(result).toEqual(
      expect.objectContaining({
        action: 'Allow',
        profileId: 'lossless-exact',
        status: 'Verified',
      }),
    );
    expect(result.confidence).toBeGreaterThanOrEqual(0.9);
  });

  it('quarantines failed strict verification without blocking manual access', () => {
    const result = buildAudioVerificationDecision({
      expected: {
        durationMs: 180_000,
        sha256: 'expected',
      },
      file: {
        durationMs: 230_000,
        fileName: 'track.flac',
        size: 500_000,
        type: 'audio/mpeg',
      },
      fingerprint: {
        status: 'Verified',
        value: 'actual',
      },
      profileId: 'lossless-exact',
    });

    expect(result.action).toBe('Quarantine');
    expect(result.status).toBe('Failed');
    expect(result.warnings).toEqual(
      expect.arrayContaining([
        'HashDb expected SHA-256 does not match.',
        'Duration differs from expected metadata.',
        'Strict profile expected a lossless codec/container hint.',
      ]),
    );
  });

  it('caches browser fingerprints for repeated verification', async () => {
    const fileOptions = { lastModified: 123, type: 'audio/flac' };
    const first = await verifyAudioFile(new File(['abc'], 'track.flac', fileOptions), {
      profileId: 'balanced',
    });
    const second = await verifyAudioFile(new File(['abc'], 'track.flac', fileOptions), {
      profileId: 'balanced',
    });

    expect(first.fingerprint.cacheHit).toBe(false);
    expect(second.fingerprint.cacheHit).toBe(true);
    expect(Object.keys(getAudioVerificationCache())).toHaveLength(1);

    clearAudioVerificationCache();
    expect(JSON.parse(localStorage.getItem(audioVerificationCacheStorageKey))).toEqual({});
  });
});
