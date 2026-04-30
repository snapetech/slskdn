const toHex = (buffer) =>
  Array.from(new Uint8Array(buffer))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');

export const fingerprintFile = async (file) => {
  if (!globalThis.crypto?.subtle) {
    return {
      algorithm: 'sha256',
      error: 'Web Crypto digest support is unavailable in this browser.',
      status: 'Unavailable',
    };
  }

  const digest = await globalThis.crypto.subtle.digest(
    'SHA-256',
    await file.arrayBuffer(),
  );

  return {
    algorithm: 'sha256',
    size: file.size,
    status: 'Verified',
    value: toHex(digest),
    verifiedAt: new Date().toISOString(),
  };
};
