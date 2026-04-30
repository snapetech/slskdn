import { fingerprintFile } from './fileFingerprint';

describe('fileFingerprint', () => {
  it('hashes selected files with SHA-256', async () => {
    const result = await fingerprintFile(new File(['abc'], 'track.flac'));

    expect(result).toEqual(
      expect.objectContaining({
        algorithm: 'sha256',
        size: 3,
        status: 'Verified',
        value: 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
      }),
    );
  });
});
