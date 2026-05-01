const toHex = (buffer) =>
  Array.from(new Uint8Array(buffer))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');

const isArrayBuffer = (value) =>
  Object.prototype.toString.call(value) === '[object ArrayBuffer]';

const toDigestInput = (value) => {
  if (isArrayBuffer(value) || ArrayBuffer.isView(value)) {
    return value;
  }

  if (value?.buffer && isArrayBuffer(value.buffer)) {
    return new Uint8Array(
      value.buffer,
      value.byteOffset || 0,
      value.byteLength ?? value.buffer.byteLength,
    );
  }

  return new Uint8Array(value);
};

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
    toDigestInput(await file.arrayBuffer()),
  );

  return {
    algorithm: 'sha256',
    size: file.size,
    status: 'Verified',
    value: toHex(digest),
    verifiedAt: new Date().toISOString(),
  };
};
