import { fingerprintFile } from './fileFingerprint';
import { getLocalStorageItem, setLocalStorageItem } from './storage';

export const audioVerificationCacheStorageKey = 'slskdn.audioVerification.cache';

export const audioVerificationProfiles = [
  {
    codecStrict: true,
    enabled: true,
    failMode: 'fail-closed',
    id: 'lossless-exact',
    minConfidence: 0.9,
    title: 'Lossless Exact',
  },
  {
    codecStrict: false,
    enabled: true,
    failMode: 'fail-open',
    id: 'balanced',
    minConfidence: 0.7,
    title: 'Balanced',
  },
  {
    codecStrict: false,
    enabled: true,
    failMode: 'fail-open',
    id: 'permissive',
    minConfidence: 0.5,
    title: 'Permissive',
  },
];

const losslessExtensions = new Set(['aiff', 'alac', 'flac', 'wav']);
const lossyMimeHints = ['mp3', 'mpeg', 'ogg', 'opus', 'aac'];

const now = () => new Date().toISOString();

const getExtension = (fileName = '') => {
  const lastDot = fileName.lastIndexOf('.');
  return lastDot > 0 ? fileName.slice(lastDot + 1).toLowerCase() : '';
};

const normalizeProfile = (profileId) =>
  audioVerificationProfiles.find((profile) => profile.id === profileId) ||
  audioVerificationProfiles[1];

const getCacheKey = (file) =>
  [
    file.name || file.fileName || 'unknown',
    file.size || 0,
    file.lastModified || '',
  ].join(':');

const readCache = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(audioVerificationCacheStorageKey, '{}'));
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
};

const saveCache = (cache, setItem = setLocalStorageItem) => {
  setItem(audioVerificationCacheStorageKey, JSON.stringify(cache));
  return cache;
};

export const getAudioVerificationCache = () => readCache();

export const clearAudioVerificationCache = () => saveCache({});

const getCachedFingerprint = async (
  file,
  {
    cacheEnabled = true,
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const key = getCacheKey(file);
  const cache = readCache(getItem);
  if (cacheEnabled && cache[key]) {
    return {
      ...cache[key],
      cacheHit: true,
    };
  }

  const fingerprint = await fingerprintFile(file);
  const cached = {
    ...fingerprint,
    cacheHit: false,
    cacheKey: key,
  };

  if (cacheEnabled && fingerprint.status === 'Verified') {
    saveCache(
      {
        ...cache,
        [key]: cached,
      },
      setItem,
    );
  }

  return cached;
};

export const buildAudioVerificationDecision = ({
  expected = {},
  file = {},
  fingerprint = null,
  profileId = 'balanced',
} = {}) => {
  const profile = normalizeProfile(profileId);
  const extension = getExtension(file.name || file.fileName);
  const mime = `${file.type || ''}`.toLowerCase();
  const evidence = [];
  const warnings = [];
  let confidence = 0.25;

  if (!profile.enabled) {
    return {
      action: 'Review',
      confidence: 0,
      evidence: ['Audio verification profile is disabled.'],
      failMode: profile.failMode,
      profileId: profile.id,
      status: 'Disabled',
      warnings: [],
    };
  }

  if (fingerprint?.status === 'Verified') {
    confidence += 0.35;
    evidence.push(`SHA-256 fingerprint ${fingerprint.value.slice(0, 12)} captured.`);
  } else if (fingerprint?.status) {
    warnings.push(`Fingerprint ${fingerprint.status.toLowerCase()}: ${fingerprint.error || 'not available'}`);
  } else {
    warnings.push('Fingerprint evidence is missing.');
  }

  if (expected.sha256 && fingerprint?.value === expected.sha256) {
    confidence += 0.25;
    evidence.push('HashDb expected SHA-256 matches exactly.');
  } else if (expected.sha256) {
    confidence -= 0.25;
    warnings.push('HashDb expected SHA-256 does not match.');
  }

  if (expected.durationMs && file.durationMs) {
    const delta = Math.abs(expected.durationMs - file.durationMs);
    if (delta <= 2_000) {
      confidence += 0.15;
      evidence.push('Duration matches within 2 seconds.');
    } else if (delta <= 5_000) {
      confidence += 0.06;
      warnings.push('Duration is close but should be reviewed.');
    } else {
      confidence -= 0.18;
      warnings.push('Duration differs from expected metadata.');
    }
  } else {
    warnings.push('Duration sanity evidence is unavailable.');
  }

  const losslessExtension = losslessExtensions.has(extension);
  const lossyMime = lossyMimeHints.some((hint) => mime.includes(hint));
  if (losslessExtension && !lossyMime) {
    confidence += 0.1;
    evidence.push(`Codec/container hint .${extension || 'unknown'} is acceptable.`);
  } else if (profile.codecStrict) {
    confidence -= 0.16;
    warnings.push('Strict profile expected a lossless codec/container hint.');
  }

  if (losslessExtension && file.size > 0 && file.size < 1_000_000) {
    confidence -= 0.16;
    warnings.push('Possible fake-lossless candidate: lossless extension with very small file size.');
  }

  confidence = Math.min(Math.max(confidence, 0), 0.99);
  const passed = confidence >= profile.minConfidence && warnings.length === 0;
  const status = passed ? 'Verified' : confidence >= 0.5 ? 'Review' : 'Failed';
  const action = passed
    ? 'Allow'
    : profile.failMode === 'fail-closed'
      ? 'Quarantine'
      : 'Review';

  return {
    action,
    confidence: Number(confidence.toFixed(2)),
    evidence,
    failMode: profile.failMode,
    minConfidence: profile.minConfidence,
    profileId: profile.id,
    status,
    verifiedAt: now(),
    warnings,
  };
};

export const verifyAudioFile = async (
  file,
  {
    cacheEnabled = true,
    expected = {},
    profileId = 'balanced',
  } = {},
) => {
  const fingerprint = await getCachedFingerprint(file, { cacheEnabled });
  return {
    fingerprint,
    verification: buildAudioVerificationDecision({
      expected,
      file,
      fingerprint,
      profileId,
    }),
  };
};
