const storageKey = 'slskdn.acquisitionProfile';

export const acquisitionProfiles = [
  {
    description: 'Strict lossless matching with conservative queue tolerance.',
    icon: 'gem outline',
    id: 'lossless-exact',
    label: 'Lossless Exact',
    summary: 'FLAC/WAV first, strict metadata and duration matching.',
  },
  {
    description: 'Prefer quick, good-quality matches when exact lossless is not required.',
    icon: 'bolt',
    id: 'fast-good-enough',
    label: 'Fast Good Enough',
    summary: 'High-bitrate lossy allowed, shorter queues preferred.',
  },
  {
    description: 'Prefer complete album folders with coherent release evidence.',
    icon: 'list alternate outline',
    id: 'album-complete',
    label: 'Album Complete',
    summary: 'Expected track count, folder consistency, album-first ranking.',
  },
  {
    description: 'Keep rare candidates visible but require review before risky grabs.',
    icon: 'search plus',
    id: 'rare-hunt',
    label: 'Rare Hunt',
    summary: 'Slower queues and single-source candidates stay review-first.',
  },
  {
    description: 'Minimize public network pressure and avoid aggressive retries.',
    icon: 'leaf',
    id: 'conservative-network',
    label: 'Conservative Network',
    summary: 'Lower concurrency, no automatic public-peer retries.',
  },
  {
    description: 'Prefer trusted overlay peers before public Soulseek candidates.',
    icon: 'sitemap',
    id: 'mesh-preferred',
    label: 'Mesh Preferred',
    summary: 'Trusted mesh first, public Soulseek fallback second.',
  },
  {
    description: 'Require stronger identity evidence before import decisions.',
    icon: 'certificate',
    id: 'metadata-strict',
    label: 'Metadata Strict',
    summary: 'MBID, ISRC, or fingerprint evidence drives acceptance.',
  },
];

export const defaultAcquisitionProfileId = 'lossless-exact';

export const acquisitionProfileStorageKey = storageKey;

export const getAcquisitionProfile = (id) =>
  acquisitionProfiles.find((profile) => profile.id === id) ??
  acquisitionProfiles.find((profile) => profile.id === defaultAcquisitionProfileId);

export const getStoredAcquisitionProfileId = (getItem) => {
  const stored = getItem(storageKey);
  return getAcquisitionProfile(stored)?.id ?? defaultAcquisitionProfileId;
};

export const setStoredAcquisitionProfileId = (setItem, id) => {
  const profile = getAcquisitionProfile(id);
  setItem(storageKey, profile.id);
  return profile;
};
