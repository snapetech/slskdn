import {
  getMeshEvidencePolicy,
  getMeshEvidencePolicySummary,
  meshEvidencePolicyStorageKey,
  resetMeshEvidencePolicy,
  setMeshEvidenceInboundTrustTier,
  setMeshEvidenceOutboundEnabled,
} from './meshEvidencePolicy';

describe('meshEvidencePolicy', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('defaults to private inbound and outbound mesh evidence handling', () => {
    const policy = getMeshEvidencePolicy();
    const summary = getMeshEvidencePolicySummary(policy);

    expect(policy.inboundTrustTier).toBe('disabled');
    expect(Object.values(policy.outbound).every((enabled) => !enabled)).toBe(true);
    expect(policy.provenanceRequired).toBe(true);
    expect(summary.privateByDefault).toBe(true);
  });

  it('persists inbound trust tier selection', () => {
    const policy = setMeshEvidenceInboundTrustTier('trusted');

    expect(policy.inboundTrustTier).toBe('trusted');
    expect(JSON.parse(localStorage.getItem(meshEvidencePolicyStorageKey))).toEqual(
      expect.objectContaining({
        inboundTrustTier: 'trusted',
        provenanceRequired: true,
      }),
    );
  });

  it('persists explicit outbound evidence opt-ins', () => {
    const policy = setMeshEvidenceOutboundEnabled('hashVerification', true);
    const summary = getMeshEvidencePolicySummary(policy);

    expect(policy.outbound.hashVerification).toBe(true);
    expect(policy.outbound.metadataCorrection).toBe(false);
    expect(summary.outboundEnabled).toBe(true);
    expect(summary.enabledOutbound.map((item) => item.id)).toEqual([
      'hashVerification',
    ]);
  });

  it('sanitizes invalid stored policy data', () => {
    localStorage.setItem(
      meshEvidencePolicyStorageKey,
      JSON.stringify({
        inboundTrustTier: 'everyone',
        outbound: {
          hashVerification: true,
          rawLibraryDump: true,
        },
        provenanceRequired: false,
      }),
    );

    const policy = getMeshEvidencePolicy();

    expect(policy.inboundTrustTier).toBe('disabled');
    expect(policy.outbound.hashVerification).toBe(true);
    expect(policy.outbound.rawLibraryDump).toBeUndefined();
    expect(policy.provenanceRequired).toBe(true);
  });

  it('resets policy to private defaults', () => {
    setMeshEvidenceInboundTrustTier('realm');
    setMeshEvidenceOutboundEnabled('metadataCorrection', true);

    const policy = resetMeshEvidencePolicy();

    expect(policy).toEqual(getMeshEvidencePolicy());
    expect(localStorage.getItem(meshEvidencePolicyStorageKey)).toBeNull();
  });
});
