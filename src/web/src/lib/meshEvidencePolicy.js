import {
  getLocalStorageItem,
  removeLocalStorageItem,
  setLocalStorageItem,
} from './storage';

export const meshEvidencePolicyStorageKey = 'slskdn.meshEvidencePolicy';

export const inboundTrustTiers = [
  {
    description: 'Ignore all inbound mesh evidence until explicitly enabled.',
    id: 'disabled',
    label: 'Disabled',
  },
  {
    description: 'Apply evidence only from directly trusted peers and contacts.',
    id: 'trusted',
    label: 'Trusted peers',
  },
  {
    description: 'Apply trusted peer and realm-curated evidence with provenance.',
    id: 'realm',
    label: 'Trusted realms',
  },
];

export const outboundEvidenceTypes = [
  {
    description: 'Signed statements that a known hash verified locally.',
    id: 'hashVerification',
    label: 'Hash verification',
  },
  {
    description: 'Signed release-completeness observations without file paths.',
    id: 'releaseCompleteness',
    label: 'Release completeness',
  },
  {
    description: 'Signed warnings for suspicious fake-lossless candidates.',
    id: 'fakeLosslessWarning',
    label: 'Fake-lossless warnings',
  },
  {
    description: 'Signed metadata corrections reviewed by this operator.',
    id: 'metadataCorrection',
    label: 'Metadata corrections',
  },
  {
    description: 'Realm-curated subject index entries, never raw library dumps.',
    id: 'realmSubjectIndex',
    label: 'Realm subject indexes',
  },
];

export const defaultMeshEvidencePolicy = {
  inboundTrustTier: 'disabled',
  outbound: outboundEvidenceTypes.reduce((state, evidenceType) => {
    state[evidenceType.id] = false;
    return state;
  }, {}),
  provenanceRequired: true,
  updatedAt: null,
};

const sanitizePolicy = (policy = {}) => {
  const inboundTrustTier = inboundTrustTiers.some(
    (tier) => tier.id === policy.inboundTrustTier,
  )
    ? policy.inboundTrustTier
    : defaultMeshEvidencePolicy.inboundTrustTier;

  const outbound = outboundEvidenceTypes.reduce((state, evidenceType) => {
    state[evidenceType.id] = policy.outbound?.[evidenceType.id] === true;
    return state;
  }, {});

  return {
    inboundTrustTier,
    outbound,
    provenanceRequired: true,
    updatedAt: policy.updatedAt || null,
  };
};

export const getMeshEvidencePolicy = () => {
  const stored = getLocalStorageItem(meshEvidencePolicyStorageKey);
  if (!stored) {
    return defaultMeshEvidencePolicy;
  }

  try {
    return sanitizePolicy(JSON.parse(stored));
  } catch (_error) {
    removeLocalStorageItem(meshEvidencePolicyStorageKey);
    return defaultMeshEvidencePolicy;
  }
};

export const saveMeshEvidencePolicy = (policy) => {
  const sanitized = sanitizePolicy({
    ...policy,
    updatedAt: new Date().toISOString(),
  });

  setLocalStorageItem(meshEvidencePolicyStorageKey, JSON.stringify(sanitized));
  return sanitized;
};

export const setMeshEvidenceInboundTrustTier = (inboundTrustTier) =>
  saveMeshEvidencePolicy({
    ...getMeshEvidencePolicy(),
    inboundTrustTier,
  });

export const setMeshEvidenceOutboundEnabled = (evidenceTypeId, enabled) => {
  const policy = getMeshEvidencePolicy();
  return saveMeshEvidencePolicy({
    ...policy,
    outbound: {
      ...policy.outbound,
      [evidenceTypeId]: enabled === true,
    },
  });
};

export const resetMeshEvidencePolicy = () => {
  removeLocalStorageItem(meshEvidencePolicyStorageKey);
  return defaultMeshEvidencePolicy;
};

export const getMeshEvidencePolicySummary = (policy = getMeshEvidencePolicy()) => {
  const enabledOutbound = outboundEvidenceTypes.filter(
    (evidenceType) => policy.outbound[evidenceType.id],
  );
  const inboundTier = inboundTrustTiers.find(
    (tier) => tier.id === policy.inboundTrustTier,
  );

  return {
    enabledOutbound,
    inboundEnabled: policy.inboundTrustTier !== 'disabled',
    inboundTier,
    outboundEnabled: enabledOutbound.length > 0,
    privateByDefault:
      policy.inboundTrustTier === 'disabled' && enabledOutbound.length === 0,
  };
};
