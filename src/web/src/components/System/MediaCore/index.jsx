import React, { useEffect, useState } from 'react';
import { Button, Card, Dropdown, Form, Grid, Header, Icon, Input, Label, List, Loader, Message, Segment, Statistic, TextArea } from 'semantic-ui-react';
import * as mediacore from '../../../lib/mediacore';

// Predefined examples for different domains
const contentExamples = {
  audio: {
    track: { external: 'mb:recording:12345', content: 'content:audio:track:mb-12345' },
    album: { external: 'mb:release:67890', content: 'content:audio:album:mb-67890' },
    artist: { external: 'mb:artist:abc123', content: 'content:audio:artist:mb-abc123' }
  },
  video: {
    movie: { external: 'imdb:tt0111161', content: 'content:video:movie:imdb-tt0111161' },
    series: { external: 'tvdb:series:12345', content: 'content:video:series:tvdb-12345' }
  },
  image: {
    photo: { external: 'flickr:photo:67890', content: 'content:image:photo:flickr-67890' },
    artwork: { external: 'discogs:release:11111', content: 'content:image:artwork:discogs-11111' }
  }
};

const MediaCore = () => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Form state
  const [externalId, setExternalId] = useState('');
  const [contentId, setContentId] = useState('');
  const [resolveId, setResolveId] = useState('');
  const [validateContentIdInput, setValidateContentIdInput] = useState('');
  const [domain, setDomain] = useState('');
  const [type, setType] = useState('');
  const [resolvedContent, setResolvedContent] = useState(null);
  const [validatedContent, setValidatedContent] = useState(null);
  const [domainResults, setDomainResults] = useState(null);
  const [traversalResults, setTraversalResults] = useState(null);
  const [graphResults, setGraphResults] = useState(null);
  const [inboundResults, setInboundResults] = useState(null);
  const [traverseContentId, setTraverseContentId] = useState('');
  const [traverseLinkName, setTraverseLinkName] = useState('');
  const [graphContentId, setGraphContentId] = useState('');
  const [inboundTargetId, setInboundTargetId] = useState('');
  const [registering, setRegistering] = useState(false);
  const [resolving, setResolving] = useState(false);
  const [validating, setValidating] = useState(false);
  const [searchingDomain, setSearchingDomain] = useState(false);
  const [traversing, setTraversing] = useState(false);
  const [gettingGraph, setGettingGraph] = useState(false);
  const [findingInbound, setFindingInbound] = useState(false);
  const [audioSamples, setAudioSamples] = useState('');
  const [sampleRate, setSampleRate] = useState(44100);
  const [audioAlgorithm, setAudioAlgorithm] = useState('ChromaPrint');
  const [imagePixels, setImagePixels] = useState('');
  const [imageWidth, setImageWidth] = useState(100);
  const [imageHeight, setImageHeight] = useState(100);
  const [imageAlgorithm, setImageAlgorithm] = useState('PHash');
  const [hashA, setHashA] = useState('');
  const [hashB, setHashB] = useState('');
  const [similarityThreshold, setSimilarityThreshold] = useState(0.8);
  const [audioHashResult, setAudioHashResult] = useState(null);
  const [imageHashResult, setImageHashResult] = useState(null);
  const [similarityResult, setSimilarityResult] = useState(null);
  const [supportedAlgorithms, setSupportedAlgorithms] = useState(null);
  const [computingAudioHash, setComputingAudioHash] = useState(false);
  const [computingImageHash, setComputingImageHash] = useState(false);
  const [computingSimilarity, setComputingSimilarity] = useState(false);
  const [perceptualContentIdA, setPerceptualContentIdA] = useState('');
  const [perceptualContentIdB, setPerceptualContentIdB] = useState('');
  const [perceptualThreshold, setPerceptualThreshold] = useState(0.7);
  const [findSimilarContentId, setFindSimilarContentId] = useState('');
  const [findSimilarMinConfidence, setFindSimilarMinConfidence] = useState(0.7);
  const [findSimilarMaxResults, setFindSimilarMaxResults] = useState(10);
  const [textSimilarityA, setTextSimilarityA] = useState('');
  const [textSimilarityB, setTextSimilarityB] = useState('');
  const [perceptualSimilarityResult, setPerceptualSimilarityResult] = useState(null);
  const [findSimilarResult, setFindSimilarResult] = useState(null);
  const [textSimilarityResult, setTextSimilarityResult] = useState(null);
  const [computingPerceptualSimilarity, setComputingPerceptualSimilarity] = useState(false);
  const [findingSimilarContent, setFindingSimilarContent] = useState(false);
  const [computingTextSimilarity, setComputingTextSimilarity] = useState(false);
  const [exportContentIds, setExportContentIds] = useState('');
  const [includeLinks, setIncludeLinks] = useState(true);
  const [importPackage, setImportPackage] = useState('');
  const [conflictStrategy, setConflictStrategy] = useState('Merge');
  const [dryRun, setDryRun] = useState(false);
  const [exportResult, setExportResult] = useState(null);
  const [importResult, setImportResult] = useState(null);
  const [conflictAnalysis, setConflictAnalysis] = useState(null);
  const [availableStrategies, setAvailableStrategies] = useState(null);
  const [exportingMetadata, setExportingMetadata] = useState(false);
  const [importingMetadata, setImportingMetadata] = useState(false);
  const [analyzingConflicts, setAnalyzingConflicts] = useState(false);
  const [retrievalResult, setRetrievalResult] = useState(null);
  const [batchRetrievalResult, setBatchRetrievalResult] = useState(null);
  const [queryResult, setQueryResult] = useState(null);
  const [verificationResult, setVerificationResult] = useState(null);
  const [retrievalStats, setRetrievalStats] = useState(null);
  const [retrieveContentId, setRetrieveContentId] = useState('');
  const [batchRetrieveContentIds, setBatchRetrieveContentIds] = useState('');
  const [queryDomain, setQueryDomain] = useState('audio');
  const [queryType, setQueryType] = useState('');
  const [queryMaxResults, setQueryMaxResults] = useState(50);
  const [verifyDescriptor, setVerifyDescriptor] = useState('');
  const [bypassCache, setBypassCache] = useState(false);
  const [retrievingDescriptor, setRetrievingDescriptor] = useState(false);
  const [retrievingBatch, setRetrievingBatch] = useState(false);
  const [queryingDescriptors, setQueryingDescriptors] = useState(false);
  const [verifyingDescriptor, setVerifyingDescriptor] = useState(false);
  const [loadingRetrievalStats, setLoadingRetrievalStats] = useState(false);
  const [mediaCoreDashboard, setMediaCoreDashboard] = useState(null);
  const [contentRegistryStats, setContentRegistryStats] = useState(null);
  const [descriptorStats, setDescriptorStats] = useState(null);
  const [fuzzyMatchingStats, setFuzzyMatchingStats] = useState(null);
  const [ipldMappingStats, setIpldMappingStats] = useState(null);
  const [perceptualHashingStats, setPerceptualHashingStats] = useState(null);
  const [metadataPortabilityStats, setMetadataPortabilityStats] = useState(null);
  const [contentPublishingStats, setContentPublishingStats] = useState(null);
  const [loadingDashboard, setLoadingDashboard] = useState(false);
  const [loadingRegistryStats, setLoadingRegistryStats] = useState(false);
  const [loadingDescriptorStats, setLoadingDescriptorStats] = useState(false);
  const [loadingFuzzyStats, setLoadingFuzzyStats] = useState(false);
  const [loadingIpldStats, setLoadingIpldStats] = useState(false);
  const [loadingPerceptualStats, setLoadingPerceptualStats] = useState(false);
  const [loadingPortabilityStats, setLoadingPortabilityStats] = useState(false);
  const [loadingPublishingStats, setLoadingPublishingStats] = useState(false);

  // PodCore DHT states
  const [podToPublish, setPodToPublish] = useState('');
  const [publishingPod, setPublishingPod] = useState(false);
  const [podPublishingResult, setPodPublishingResult] = useState(null);
  const [podMetadataToRetrieve, setPodMetadataToRetrieve] = useState('');
  const [retrievingPodMetadata, setRetrievingPodMetadata] = useState(false);
  const [podMetadataResult, setPodMetadataResult] = useState(null);
  const [podToUnpublish, setPodToUnpublish] = useState('');
  const [unpublishingPod, setUnpublishingPod] = useState(false);
  const [podUnpublishResult, setPodUnpublishResult] = useState(null);
  const [podPublishingStats, setPodPublishingStats] = useState(null);
  const [loadingPodStats, setLoadingPodStats] = useState(false);

  // Pod Membership states
  const [membershipRecord, setMembershipRecord] = useState('');
  const [publishingMembership, setPublishingMembership] = useState(false);
  const [membershipPublishResult, setMembershipPublishResult] = useState(null);
  const [membershipPodId, setMembershipPodId] = useState('');
  const [membershipPeerId, setMembershipPeerId] = useState('');
  const [gettingMembership, setGettingMembership] = useState(false);
  const [membershipResult, setMembershipResult] = useState(null);
  const [verifyingMembership, setVerifyingMembership] = useState(false);
  const [membershipVerification, setMembershipVerification] = useState(null);
  const [banningMember, setBanningMember] = useState(false);
  const [banReason, setBanReason] = useState('');
  const [banResult, setBanResult] = useState(null);
  const [changingRole, setChangingRole] = useState(false);
  const [newRole, setNewRole] = useState('member');
  const [roleChangeResult, setRoleChangeResult] = useState(null);
  const [membershipStats, setMembershipStats] = useState(null);
  const [loadingMembershipStats, setLoadingMembershipStats] = useState(false);

  // Pod Membership Verification states
  const [verifyPodId, setVerifyPodId] = useState('');
  const [verifyPeerId, setVerifyPeerId] = useState('');
  const [verifyingMembership, setVerifyingMembership] = useState(false);
  const [membershipVerificationResult, setMembershipVerificationResult] = useState(null);
  const [messageToVerify, setMessageToVerify] = useState('');
  const [verifyingMessage, setVerifyingMessage] = useState(false);
  const [messageVerificationResult, setMessageVerificationResult] = useState(null);
  const [roleCheckPodId, setRoleCheckPodId] = useState('');
  const [roleCheckPeerId, setRoleCheckPeerId] = useState('');
  const [requiredRole, setRequiredRole] = useState('member');
  const [checkingRole, setCheckingRole] = useState(false);
  const [roleCheckResult, setRoleCheckResult] = useState(null);
  const [verificationStats, setVerificationStats] = useState(null);
  const [loadingVerificationStats, setLoadingVerificationStats] = useState(false);

  // Pod Discovery states
  const [podToRegister, setPodToRegister] = useState('');
  const [registeringPod, setRegisteringPod] = useState(false);
  const [podRegistrationResult, setPodRegistrationResult] = useState(null);
  const [podToUnregister, setPodToUnregister] = useState('');
  const [unregisteringPod, setUnregisteringPod] = useState(false);
  const [podUnregistrationResult, setPodUnregistrationResult] = useState(null);
  const [discoverByName, setDiscoverByName] = useState('');
  const [discoveringByName, setDiscoveringByName] = useState(false);
  const [nameDiscoveryResult, setNameDiscoveryResult] = useState(null);
  const [discoverByTag, setDiscoverByTag] = useState('');
  const [discoveringByTag, setDiscoveringByTag] = useState(false);
  const [tagDiscoveryResult, setTagDiscoveryResult] = useState(null);
  const [discoverTags, setDiscoverTags] = useState('');
  const [discoveringByTags, setDiscoveringByTags] = useState(false);
  const [tagsDiscoveryResult, setTagsDiscoveryResult] = useState(null);
  const [discoverLimit, setDiscoverLimit] = useState(50);
  const [discoveringAll, setDiscoveringAll] = useState(false);
  const [allDiscoveryResult, setAllDiscoveryResult] = useState(null);
  const [discoverByContent, setDiscoverByContent] = useState('');
  const [discoveringByContent, setDiscoveringByContent] = useState(false);
  const [contentDiscoveryResult, setContentDiscoveryResult] = useState(null);
  const [discoveryStats, setDiscoveryStats] = useState(null);
  const [loadingDiscoveryStats, setLoadingDiscoveryStats] = useState(false);
  const [publishContentId, setPublishContentId] = useState('');
  const [publishCodec, setPublishCodec] = useState('mp3');
  const [publishSize, setPublishSize] = useState(1024);
  const [batchContentIds, setBatchContentIds] = useState('');
  const [updateTargetId, setUpdateTargetId] = useState('');
  const [updateCodec, setUpdateCodec] = useState('');
  const [updateSize, setUpdateSize] = useState('');
  const [updateConfidence, setUpdateConfidence] = useState('');
  const [publishResult, setPublishResult] = useState(null);
  const [batchPublishResult, setBatchPublishResult] = useState(null);
  const [updateResult, setUpdateResult] = useState(null);
  const [republishResult, setRepublishResult] = useState(null);
  const [publishingStats, setPublishingStats] = useState(null);
  const [publishingDescriptor, setPublishingDescriptor] = useState(false);
  const [publishingBatch, setPublishingBatch] = useState(false);
  const [updatingDescriptor, setUpdatingDescriptor] = useState(false);
  const [republishing, setRepublishing] = useState(false);
  const [loadingStats, setLoadingStats] = useState(false);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await mediacore.getContentIdStats();
        setStats(data);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();

    // Refresh stats every 60 seconds
    const interval = setInterval(fetchStats, 60000);
    return () => clearInterval(interval);
  }, []);

  const handleRegister = async () => {
    if (!externalId.trim() || !contentId.trim()) return;

    try {
      setRegistering(true);
      await mediacore.registerContentId(externalId.trim(), contentId.trim());
      setExternalId('');
      setContentId('');

      // Refresh stats
      const data = await mediacore.getContentIdStats();
      setStats(data);
    } catch (err) {
      setError(`Failed to register: ${err.message}`);
    } finally {
      setRegistering(false);
    }
  };

  const handleResolve = async () => {
    if (!resolveId.trim()) return;

    try {
      setResolving(true);
      setResolvedContent(null);
      const result = await mediacore.resolveContentId(resolveId.trim());
      setResolvedContent(result);
    } catch (err) {
      setResolvedContent({ error: err.message });
    } finally {
      setResolving(false);
    }
  };

  const handleValidate = async () => {
    if (!validateContentIdInput.trim()) return;

    try {
      setValidating(true);
      setValidatedContent(null);
      const result = await mediacore.validateContentId(validateContentIdInput.trim());
      setValidatedContent(result);
    } catch (err) {
      setValidatedContent({ error: err.message });
    } finally {
      setValidating(false);
    }
  };

  const handleDomainSearch = async () => {
    if (!domain.trim()) return;

    try {
      setSearchingDomain(true);
      setDomainResults(null);
      const result = type.trim()
        ? await mediacore.findContentIdsByDomainAndType(domain.trim(), type.trim())
        : await mediacore.findContentIdsByDomain(domain.trim());
      setDomainResults(result);
    } catch (err) {
      setDomainResults({ error: err.message });
    } finally {
      setSearchingDomain(false);
    }
  };

  const fillExample = (domain, type) => {
    const example = contentExamples[domain]?.[type];
    if (example) {
      setExternalId(example.external);
      setContentId(example.content);
    }
  };

  const handleTraverse = async () => {
    if (!traverseContentId.trim() || !traverseLinkName.trim()) return;

    try {
      setTraversing(true);
      setTraversalResults(null);
      const result = await mediacore.traverseContentGraph(traverseContentId.trim(), traverseLinkName.trim());
      setTraversalResults(result);
    } catch (err) {
      setTraversalResults({ error: err.message });
    } finally {
      setTraversing(false);
    }
  };

  const handleGetGraph = async () => {
    if (!graphContentId.trim()) return;

    try {
      setGettingGraph(true);
      setGraphResults(null);
      const result = await mediacore.getContentGraph(graphContentId.trim());
      setGraphResults(result);
    } catch (err) {
      setGraphResults({ error: err.message });
    } finally {
      setGettingGraph(false);
    }
  };

  const handleFindInbound = async () => {
    if (!inboundTargetId.trim()) return;

    try {
      setFindingInbound(true);
      setInboundResults(null);
      const result = await mediacore.findInboundLinks(inboundTargetId.trim());
      setInboundResults(result);
    } catch (err) {
      setInboundResults({ error: err.message });
    } finally {
      setFindingInbound(false);
    }
  };

  const loadSupportedAlgorithms = async () => {
    try {
      const result = await mediacore.getSupportedHashAlgorithms();
      setSupportedAlgorithms(result);
    } catch (err) {
      console.error('Failed to load hash algorithms:', err);
    }
  };

  const handleComputeAudioHash = async () => {
    if (!audioSamples.trim()) return;

    try {
      setComputingAudioHash(true);
      setAudioHashResult(null);

      // Parse comma-separated float values
      const samples = audioSamples.split(',').map(s => parseFloat(s.trim())).filter(n => !isNaN(n));

      if (samples.length === 0) {
        throw new Error('No valid audio samples provided');
      }

      const result = await mediacore.computeAudioHash(samples, parseInt(sampleRate), audioAlgorithm);
      setAudioHashResult(result);
    } catch (err) {
      setAudioHashResult({ error: err.message });
    } finally {
      setComputingAudioHash(false);
    }
  };

  const handleComputeImageHash = async () => {
    if (!imagePixels.trim()) return;

    try {
      setComputingImageHash(true);
      setImageHashResult(null);

      // Parse comma-separated byte values (0-255)
      const pixels = imagePixels.split(',').map(s => parseInt(s.trim())).filter(n => !isNaN(n) && n >= 0 && n <= 255);

      if (pixels.length === 0) {
        throw new Error('No valid pixel data provided');
      }

      const result = await mediacore.computeImageHash(pixels, parseInt(imageWidth), parseInt(imageHeight), imageAlgorithm);
      setImageHashResult(result);
    } catch (err) {
      setImageHashResult({ error: err.message });
    } finally {
      setComputingImageHash(false);
    }
  };

  const handleComputeSimilarity = async () => {
    if (!hashA.trim() || !hashB.trim()) return;

    try {
      setComputingSimilarity(true);
      setSimilarityResult(null);
      const result = await mediacore.computeHashSimilarity(hashA.trim(), hashB.trim(), parseFloat(similarityThreshold));
      setSimilarityResult(result);
    } catch (err) {
      setSimilarityResult({ error: err.message });
    } finally {
      setComputingSimilarity(false);
    }
  };

  const handleComputePerceptualSimilarity = async () => {
    if (!perceptualContentIdA.trim() || !perceptualContentIdB.trim()) return;

    try {
      setComputingPerceptualSimilarity(true);
      setPerceptualSimilarityResult(null);
      const result = await mediacore.computePerceptualSimilarity(
        perceptualContentIdA.trim(),
        perceptualContentIdB.trim(),
        parseFloat(perceptualThreshold)
      );
      setPerceptualSimilarityResult(result);
    } catch (err) {
      setPerceptualSimilarityResult({ error: err.message });
    } finally {
      setComputingPerceptualSimilarity(false);
    }
  };

  const handleFindSimilarContent = async () => {
    if (!findSimilarContentId.trim()) return;

    try {
      setFindingSimilarContent(true);
      setFindSimilarResult(null);
      const result = await mediacore.findSimilarContent(findSimilarContentId.trim(), {
        minConfidence: parseFloat(findSimilarMinConfidence),
        maxResults: parseInt(findSimilarMaxResults)
      });
      setFindSimilarResult(result);
    } catch (err) {
      setFindSimilarResult({ error: err.message });
    } finally {
      setFindingSimilarContent(false);
    }
  };

  const handleComputeTextSimilarity = async () => {
    if (!textSimilarityA.trim() || !textSimilarityB.trim()) return;

    try {
      setComputingTextSimilarity(true);
      setTextSimilarityResult(null);
      const result = await mediacore.computeTextSimilarity(textSimilarityA.trim(), textSimilarityB.trim());
      setTextSimilarityResult(result);
    } catch (err) {
      setTextSimilarityResult({ error: err.message });
    } finally {
      setComputingTextSimilarity(false);
    }
  };

  const handleExportMetadata = async () => {
    const contentIds = exportContentIds.split('\n').map(id => id.trim()).filter(id => id);
    if (!contentIds.length) return;

    try {
      setExportingMetadata(true);
      setExportResult(null);
      const result = await mediacore.exportMetadata(contentIds, includeLinks);
      setExportResult(result);
    } catch (err) {
      setExportResult({ error: err.message });
    } finally {
      setExportingMetadata(false);
    }
  };

  const handleImportMetadata = async () => {
    if (!importPackage.trim()) return;

    try {
      setImportingMetadata(true);
      setImportResult(null);

      let package;
      try {
        package = JSON.parse(importPackage.trim());
      } catch (parseErr) {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.importMetadata(package, conflictStrategy, dryRun);
      setImportResult(result);
    } catch (err) {
      setImportResult({ error: err.message });
    } finally {
      setImportingMetadata(false);
    }
  };

  const handleAnalyzeConflicts = async () => {
    if (!importPackage.trim()) return;

    try {
      setAnalyzingConflicts(true);
      setConflictAnalysis(null);

      let package;
      try {
        package = JSON.parse(importPackage.trim());
      } catch (parseErr) {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.analyzeMetadataConflicts(package);
      setConflictAnalysis(result);
    } catch (err) {
      setConflictAnalysis({ error: err.message });
    } finally {
      setAnalyzingConflicts(false);
    }
  };

  const handlePublishDescriptor = async () => {
    if (!publishContentId.trim()) return;

    try {
      setPublishingDescriptor(true);
      setPublishResult(null);

      const descriptor = {
        contentId: publishContentId.trim(),
        sizeBytes: parseInt(publishSize),
        codec: publishCodec.trim(),
        confidence: 0.8
      };

      const result = await mediacore.publishContentDescriptor(descriptor);
      setPublishResult(result);
    } catch (err) {
      setPublishResult({ error: err.message });
    } finally {
      setPublishingDescriptor(false);
    }
  };

  const handlePublishBatch = async () => {
    const contentIds = batchContentIds.split('\n').map(id => id.trim()).filter(id => id);
    if (!contentIds.length) return;

    try {
      setPublishingBatch(true);
      setBatchPublishResult(null);

      // Create mock descriptors for each ContentID
      const descriptors = contentIds.map(contentId => ({
        contentId,
        sizeBytes: 1024 * 1024, // 1MB mock
        codec: 'mock',
        confidence: 0.8
      }));

      const result = await mediacore.publishContentDescriptorsBatch(descriptors);
      setBatchPublishResult(result);
    } catch (err) {
      setBatchPublishResult({ error: err.message });
    } finally {
      setPublishingBatch(false);
    }
  };

  const handleUpdateDescriptor = async () => {
    if (!updateTargetId.trim()) return;

    try {
      setUpdatingDescriptor(true);
      setUpdateResult(null);

      const updates = {};
      if (updateCodec.trim()) updates.newCodec = updateCodec.trim();
      if (updateSize.trim()) updates.newSizeBytes = parseInt(updateSize);
      if (updateConfidence.trim()) updates.newConfidence = parseFloat(updateConfidence);

      if (Object.keys(updates).length === 0) {
        throw new Error('At least one update field is required');
      }

      const result = await mediacore.updateContentDescriptor(updateTargetId.trim(), updates);
      setUpdateResult(result);
    } catch (err) {
      setUpdateResult({ error: err.message });
    } finally {
      setUpdatingDescriptor(false);
    }
  };

  const handleRepublishExpiring = async () => {
    try {
      setRepublishing(true);
      setRepublishResult(null);
      const result = await mediacore.republishExpiringDescriptors();
      setRepublishResult(result);
    } catch (err) {
      setRepublishResult({ error: err.message });
    } finally {
      setRepublishing(false);
    }
  };

  const handleLoadPublishingStats = async () => {
    try {
      setLoadingStats(true);
      setPublishingStats(null);
      const result = await mediacore.getPublishingStats();
      setPublishingStats(result);
    } catch (err) {
      setPublishingStats({ error: err.message });
    } finally {
      setLoadingStats(false);
    }
  };

  const handleRetrieveDescriptor = async () => {
    if (!retrieveContentId.trim()) return;

    try {
      setRetrievingDescriptor(true);
      setRetrievalResult(null);
      const result = await mediacore.retrieveContentDescriptor(retrieveContentId.trim(), bypassCache);
      setRetrievalResult(result);
    } catch (err) {
      setRetrievalResult({ error: err.message });
    } finally {
      setRetrievingDescriptor(false);
    }
  };

  const handleRetrieveBatch = async () => {
    const contentIds = batchRetrieveContentIds.split('\n').map(id => id.trim()).filter(id => id);
    if (!contentIds.length) return;

    try {
      setRetrievingBatch(true);
      setBatchRetrievalResult(null);
      const result = await mediacore.retrieveContentDescriptorsBatch(contentIds);
      setBatchRetrievalResult(result);
    } catch (err) {
      setBatchRetrievalResult({ error: err.message });
    } finally {
      setRetrievingBatch(false);
    }
  };

  const handleQueryDescriptors = async () => {
    if (!queryDomain.trim()) return;

    try {
      setQueryingDescriptors(true);
      setQueryResult(null);
      const result = await mediacore.queryDescriptorsByDomain(
        queryDomain.trim(),
        queryType.trim() || null,
        parseInt(queryMaxResults)
      );
      setQueryResult(result);
    } catch (err) {
      setQueryResult({ error: err.message });
    } finally {
      setQueryingDescriptors(false);
    }
  };

  const handleVerifyDescriptor = async () => {
    if (!verifyDescriptor.trim()) return;

    try {
      setVerifyingDescriptor(true);
      setVerificationResult(null);

      let descriptor;
      try {
        descriptor = JSON.parse(verifyDescriptor.trim());
      } catch (parseErr) {
        throw new Error('Invalid JSON format for descriptor');
      }

      const result = await mediacore.verifyContentDescriptor(descriptor);
      setVerificationResult(result);
    } catch (err) {
      setVerificationResult({ error: err.message });
    } finally {
      setVerifyingDescriptor(false);
    }
  };

  const handleLoadRetrievalStats = async () => {
    try {
      setLoadingRetrievalStats(true);
      setRetrievalStats(null);
      const result = await mediacore.getRetrievalStats();
      setRetrievalStats(result);
    } catch (err) {
      setRetrievalStats({ error: err.message });
    } finally {
      setLoadingRetrievalStats(false);
    }
  };

  const handleClearRetrievalCache = async () => {
    try {
      const result = await mediacore.clearRetrievalCache();
      // Reload stats to reflect changes
      await handleLoadRetrievalStats();
      alert(`Cache cleared: ${result.entriesCleared} entries, ${result.bytesFreed} bytes freed`);
    } catch (err) {
      alert(`Failed to clear cache: ${err.message}`);
    }
  };

  const handleLoadMediaCoreDashboard = async () => {
    try {
      setLoadingDashboard(true);
      setMediaCoreDashboard(null);
      const result = await mediacore.getMediaCoreDashboard();
      setMediaCoreDashboard(result);
    } catch (err) {
      setMediaCoreDashboard({ error: err.message });
    } finally {
      setLoadingDashboard(false);
    }
  };

  const handleLoadContentRegistryStats = async () => {
    try {
      setLoadingRegistryStats(true);
      setContentRegistryStats(null);
      const result = await mediacore.getContentRegistryStats();
      setContentRegistryStats(result);
    } catch (err) {
      setContentRegistryStats({ error: err.message });
    } finally {
      setLoadingRegistryStats(false);
    }
  };

  const handleLoadDescriptorStats = async () => {
    try {
      setLoadingDescriptorStats(true);
      setDescriptorStats(null);
      const result = await mediacore.getDescriptorStats();
      setDescriptorStats(result);
    } catch (err) {
      setDescriptorStats({ error: err.message });
    } finally {
      setLoadingDescriptorStats(false);
    }
  };

  const handleLoadFuzzyMatchingStats = async () => {
    try {
      setLoadingFuzzyStats(true);
      setFuzzyMatchingStats(null);
      const result = await mediacore.getFuzzyMatchingStats();
      setFuzzyMatchingStats(result);
    } catch (err) {
      setFuzzyMatchingStats({ error: err.message });
    } finally {
      setLoadingFuzzyStats(false);
    }
  };

  const handleLoadIpldMappingStats = async () => {
    try {
      setLoadingIpldStats(true);
      setIpldMappingStats(null);
      const result = await mediacore.getIpldMappingStats();
      setIpldMappingStats(result);
    } catch (err) {
      setIpldMappingStats({ error: err.message });
    } finally {
      setLoadingIpldStats(false);
    }
  };

  const handleLoadPerceptualHashingStats = async () => {
    try {
      setLoadingPerceptualStats(true);
      setPerceptualHashingStats(null);
      const result = await mediacore.getPerceptualHashingStats();
      setPerceptualHashingStats(result);
    } catch (err) {
      setPerceptualHashingStats({ error: err.message });
    } finally {
      setLoadingPerceptualStats(false);
    }
  };

  const handleLoadMetadataPortabilityStats = async () => {
    try {
      setLoadingPortabilityStats(true);
      setMetadataPortabilityStats(null);
      const result = await mediacore.getMetadataPortabilityStats();
      setMetadataPortabilityStats(result);
    } catch (err) {
      setMetadataPortabilityStats({ error: err.message });
    } finally {
      setLoadingPortabilityStats(false);
    }
  };

  const handleLoadContentPublishingStats = async () => {
    try {
      setLoadingPublishingStats(true);
      setContentPublishingStats(null);
      const result = await mediacore.getContentPublishingStats();
      setContentPublishingStats(result);
    } catch (err) {
      setContentPublishingStats({ error: err.message });
    } finally {
      setLoadingPublishingStats(false);
    }
  };

  const handleResetMediaCoreStats = async () => {
    if (!confirm('Are you sure you want to reset all MediaCore statistics? This cannot be undone.')) {
      return;
    }

    try {
      await mediacore.resetMediaCoreStats();
      // Clear all displayed stats
      setMediaCoreDashboard(null);
      setContentRegistryStats(null);
      setDescriptorStats(null);
      setFuzzyMatchingStats(null);
      setIpldMappingStats(null);
      setPerceptualHashingStats(null);
      setMetadataPortabilityStats(null);
      setContentPublishingStats(null);
      alert('MediaCore statistics have been reset');
    } catch (err) {
      alert(`Failed to reset stats: ${err.message}`);
    }
  };

  // PodCore handlers
  const handlePublishPod = async () => {
    if (!podToPublish.trim()) {
      alert('Please enter pod JSON data');
      return;
    }

    try {
      setPublishingPod(true);
      setPodPublishingResult(null);
      const pod = JSON.parse(podToPublish);
      const result = await mediacore.publishPod(pod);
      setPodPublishingResult(result);
      setPodToPublish('');
    } catch (err) {
      setPodPublishingResult({ error: err.message });
    } finally {
      setPublishingPod(false);
    }
  };

  const handleRetrievePodMetadata = async () => {
    if (!podMetadataToRetrieve.trim()) {
      alert('Please enter a pod ID');
      return;
    }

    try {
      setRetrievingPodMetadata(true);
      setPodMetadataResult(null);
      const result = await mediacore.getPublishedPodMetadata(podMetadataToRetrieve);
      setPodMetadataResult(result);
    } catch (err) {
      setPodMetadataResult({ error: err.message });
    } finally {
      setRetrievingPodMetadata(false);
    }
  };

  const handleUnpublishPod = async () => {
    if (!podToUnpublish.trim()) {
      alert('Please enter a pod ID');
      return;
    }

    if (!confirm(`Are you sure you want to unpublish pod "${podToUnpublish}"?`)) {
      return;
    }

    try {
      setUnpublishingPod(true);
      setPodUnpublishResult(null);
      const result = await mediacore.unpublishPod(podToUnpublish);
      setPodUnpublishResult(result);
      setPodToUnpublish('');
    } catch (err) {
      setPodUnpublishResult({ error: err.message });
    } finally {
      setUnpublishingPod(false);
    }
  };

  const handleLoadPodPublishingStats = async () => {
    try {
      setLoadingPodStats(true);
      setPodPublishingStats(null);
      const result = await mediacore.getPodPublishingStats();
      setPodPublishingStats(result);
    } catch (err) {
      setPodPublishingStats({ error: err.message });
    } finally {
      setLoadingPodStats(false);
    }
  };

  // Pod Membership handlers
  const handlePublishMembership = async () => {
    if (!membershipRecord.trim()) {
      alert('Please enter membership record JSON data');
      return;
    }

    try {
      setPublishingMembership(true);
      setMembershipPublishResult(null);
      const record = JSON.parse(membershipRecord);
      const result = await mediacore.publishMembership(record);
      setMembershipPublishResult(result);
      setMembershipRecord('');
    } catch (err) {
      setMembershipPublishResult({ error: err.message });
    } finally {
      setPublishingMembership(false);
    }
  };

  const handleGetMembership = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setGettingMembership(true);
      setMembershipResult(null);
      const result = await mediacore.getMembership(membershipPodId, membershipPeerId);
      setMembershipResult(result);
    } catch (err) {
      setMembershipResult({ error: err.message });
    } finally {
      setGettingMembership(false);
    }
  };

  const handleVerifyMembership = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setVerifyingMembership(true);
      setMembershipVerification(null);
      const result = await mediacore.verifyMembership(membershipPodId, membershipPeerId);
      setMembershipVerification(result);
    } catch (err) {
      setMembershipVerification({ error: err.message });
    } finally {
      setVerifyingMembership(false);
    }
  };

  const handleBanMember = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    if (!confirm(`Are you sure you want to ban member "${membershipPeerId}" from pod "${membershipPodId}"?`)) {
      return;
    }

    try {
      setBanningMember(true);
      setBanResult(null);
      const result = await mediacore.banMember(membershipPodId, membershipPeerId, banReason || null);
      setBanResult(result);
      setBanReason('');
    } catch (err) {
      setBanResult({ error: err.message });
    } finally {
      setBanningMember(false);
    }
  };

  const handleChangeRole = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setChangingRole(true);
      setRoleChangeResult(null);
      const result = await mediacore.changeMemberRole(membershipPodId, membershipPeerId, newRole);
      setRoleChangeResult(result);
    } catch (err) {
      setRoleChangeResult({ error: err.message });
    } finally {
      setChangingRole(false);
    }
  };

  const handleLoadMembershipStats = async () => {
    try {
      setLoadingMembershipStats(true);
      setMembershipStats(null);
      const result = await mediacore.getMembershipStats();
      setMembershipStats(result);
    } catch (err) {
      setMembershipStats({ error: err.message });
    } finally {
      setLoadingMembershipStats(false);
    }
  };

  const handleCleanupMemberships = async () => {
    if (!confirm('Are you sure you want to cleanup expired membership records?')) {
      return;
    }

    try {
      const result = await mediacore.cleanupExpiredMemberships();
      alert(`Cleanup completed: ${result.recordsCleaned} records cleaned, ${result.errorsEncountered} errors`);
      // Reload stats to reflect changes
      await handleLoadMembershipStats();
    } catch (err) {
      alert(`Failed to cleanup: ${err.message}`);
    }
  };

  // Pod Membership Verification handlers
  const handleVerifyMembership = async () => {
    if (!verifyPodId.trim() || !verifyPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setVerifyingMembership(true);
      setMembershipVerificationResult(null);
      const result = await mediacore.verifyPodMembership(verifyPodId, verifyPeerId);
      setMembershipVerificationResult(result);
    } catch (err) {
      setMembershipVerificationResult({ error: err.message });
    } finally {
      setVerifyingMembership(false);
    }
  };

  const handleVerifyMessage = async () => {
    if (!messageToVerify.trim()) {
      alert('Please enter a message JSON');
      return;
    }

    try {
      setVerifyingMessage(true);
      setMessageVerificationResult(null);
      const message = JSON.parse(messageToVerify);
      const result = await mediacore.verifyPodMessage(message);
      setMessageVerificationResult(result);
    } catch (err) {
      setMessageVerificationResult({ error: err.message });
    } finally {
      setVerifyingMessage(false);
    }
  };

  const handleCheckRole = async () => {
    if (!roleCheckPodId.trim() || !roleCheckPeerId.trim()) {
      alert('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setCheckingRole(true);
      setRoleCheckResult(null);
      const hasRole = await mediacore.checkPodRole(roleCheckPodId, roleCheckPeerId, requiredRole);
      setRoleCheckResult({ hasRole });
    } catch (err) {
      setRoleCheckResult({ error: err.message });
    } finally {
      setCheckingRole(false);
    }
  };

  const handleLoadVerificationStats = async () => {
    try {
      setLoadingVerificationStats(true);
      setVerificationStats(null);
      const result = await mediacore.getVerificationStats();
      setVerificationStats(result);
    } catch (err) {
      setVerificationStats({ error: err.message });
    } finally {
      setLoadingVerificationStats(false);
    }
  };

  // Pod Discovery handlers
  const handleRegisterPodForDiscovery = async () => {
    if (!podToRegister.trim()) {
      alert('Please enter pod JSON data');
      return;
    }

    try {
      setRegisteringPod(true);
      setPodRegistrationResult(null);
      const pod = JSON.parse(podToRegister);
      const result = await mediacore.registerPodForDiscovery(pod);
      setPodRegistrationResult(result);
      setPodToRegister('');
    } catch (err) {
      setPodRegistrationResult({ error: err.message });
    } finally {
      setRegisteringPod(false);
    }
  };

  const handleUnregisterPodFromDiscovery = async () => {
    if (!podToUnregister.trim()) {
      alert('Please enter a pod ID');
      return;
    }

    try {
      setUnregisteringPod(true);
      setPodUnregistrationResult(null);
      const result = await mediacore.unregisterPodFromDiscovery(podToUnregister);
      setPodUnregistrationResult(result);
      setPodToUnregister('');
    } catch (err) {
      setPodUnregistrationResult({ error: err.message });
    } finally {
      setUnregisteringPod(false);
    }
  };

  const handleDiscoverByName = async () => {
    if (!discoverByName.trim()) {
      alert('Please enter a pod name');
      return;
    }

    try {
      setDiscoveringByName(true);
      setNameDiscoveryResult(null);
      const result = await mediacore.discoverPodsByName(discoverByName);
      setNameDiscoveryResult(result);
    } catch (err) {
      setNameDiscoveryResult({ error: err.message });
    } finally {
      setDiscoveringByName(false);
    }
  };

  const handleDiscoverByTag = async () => {
    if (!discoverByTag.trim()) {
      alert('Please enter a tag');
      return;
    }

    try {
      setDiscoveringByTag(true);
      setTagDiscoveryResult(null);
      const result = await mediacore.discoverPodsByTag(discoverByTag);
      setTagDiscoveryResult(result);
    } catch (err) {
      setTagDiscoveryResult({ error: err.message });
    } finally {
      setDiscoveringByTag(false);
    }
  };

  const handleDiscoverByTags = async () => {
    if (!discoverTags.trim()) {
      alert('Please enter tags (comma-separated)');
      return;
    }

    try {
      setDiscoveringByTags(true);
      setTagsDiscoveryResult(null);
      const tagList = discoverTags.split(',').map(t => t.trim()).filter(t => t);
      const result = await mediacore.discoverPodsByTags(tagList);
      setTagsDiscoveryResult(result);
    } catch (err) {
      setTagsDiscoveryResult({ error: err.message });
    } finally {
      setDiscoveringByTags(false);
    }
  };

  const handleDiscoverAll = async () => {
    try {
      setDiscoveringAll(true);
      setAllDiscoveryResult(null);
      const result = await mediacore.discoverAllPods(discoverLimit);
      setAllDiscoveryResult(result);
    } catch (err) {
      setAllDiscoveryResult({ error: err.message });
    } finally {
      setDiscoveringAll(false);
    }
  };

  const handleDiscoverByContent = async () => {
    if (!discoverByContent.trim()) {
      alert('Please enter a content ID');
      return;
    }

    try {
      setDiscoveringByContent(true);
      setContentDiscoveryResult(null);
      const result = await mediacore.discoverPodsByContent(discoverByContent);
      setContentDiscoveryResult(result);
    } catch (err) {
      setContentDiscoveryResult({ error: err.message });
    } finally {
      setDiscoveringByContent(false);
    }
  };

  const handleLoadDiscoveryStats = async () => {
    try {
      setLoadingDiscoveryStats(true);
      setDiscoveryStats(null);
      const result = await mediacore.getPodDiscoveryStats();
      setDiscoveryStats(result);
    } catch (err) {
      setDiscoveryStats({ error: err.message });
    } finally {
      setLoadingDiscoveryStats(false);
    }
  };

  const handleRefreshDiscovery = async () => {
    try {
      const result = await mediacore.refreshPodDiscovery();
      alert(`Discovery refresh completed: ${result.entriesRefreshed} refreshed, ${result.entriesExpired} expired`);
      // Reload stats to reflect changes
      await handleLoadDiscoveryStats();
    } catch (err) {
      alert(`Failed to refresh discovery: ${err.message}`);
    }
  };

  useEffect(() => {
    loadSupportedAlgorithms();
    loadAvailableStrategies();
  }, []);

  const loadAvailableStrategies = async () => {
    try {
      const result = await mediacore.getConflictStrategies();
      setAvailableStrategies(result);
    } catch (err) {
      console.error('Failed to load conflict strategies:', err);
    }
  };

  if (loading && !stats) {
    return (
      <Segment>
        <Loader active inline="centered">
          Loading MediaCore statistics...
        </Loader>
      </Segment>
    );
  }

  if (error && !stats) {
    return (
      <Message error>
        <Message.Header>Failed to load MediaCore statistics</Message.Header>
        <p>{error}</p>
      </Message>
    );
  }

  return (
    <div>
      <Header as="h2">
        <Icon name="database" />
        MediaCore ContentID Registry
      </Header>

      <Grid stackable>
        {/* Statistics Overview */}
        <Grid.Column width={16}>
          <Segment>
            <Header as="h3">Registry Statistics</Header>
            <Statistic.Group size="small">
              <Statistic>
                <Statistic.Value>{stats?.totalMappings || 0}</Statistic.Value>
                <Statistic.Label>Total Mappings</Statistic.Label>
              </Statistic>
              <Statistic>
                <Statistic.Value>{stats?.totalDomains || 0}</Statistic.Value>
                <Statistic.Label>Domains</Statistic.Label>
              </Statistic>
            </Statistic.Group>

            {stats?.mappingsByDomain && Object.keys(stats.mappingsByDomain).length > 0 && (
              <div style={{ marginTop: '1em' }}>
                <Header as="h4">Mappings by Domain</Header>
                <List horizontal>
                  {Object.entries(stats.mappingsByDomain).map(([domain, count]) => (
                    <List.Item key={domain}>
                      <Label>
                        {domain}
                        <Label.Detail>{count}</Label.Detail>
                      </Label>
                    </List.Item>
                  ))}
                </List>
              </div>
            )}
          </Segment>
        </Grid.Column>

        {/* Register New Mapping */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="plus" />
                Register ContentID Mapping
              </Card.Header>
              <Card.Description>
                Map an external identifier to an internal ContentID
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>External ID</label>
                  <Input
                    placeholder="e.g., mb:recording:12345-6789-..."
                    value={externalId}
                    onChange={(e) => setExternalId(e.target.value)}
                  />
                </Form.Field>
                <Form.Field>
                  <label>Content ID</label>
                  <Input
                    placeholder="e.g., content:mb:recording:12345-6789-..."
                    value={contentId}
                    onChange={(e) => setContentId(e.target.value)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={registering}
                  disabled={!externalId.trim() || !contentId.trim() || registering}
                  onClick={handleRegister}
                >
                  Register Mapping
                </Button>
              </Form>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Resolve External ID */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search" />
                Resolve External ID
              </Card.Header>
              <Card.Description>
                Find the ContentID for an external identifier
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>External ID to Resolve</label>
                  <Input
                    placeholder="Enter external ID to resolve..."
                    value={resolveId}
                    onChange={(e) => setResolveId(e.target.value)}
                    action={
                      <Button
                        primary
                        loading={resolving}
                        disabled={!resolveId.trim() || resolving}
                        onClick={handleResolve}
                      >
                        Resolve
                      </Button>
                    }
                  />
                </Form.Field>
              </Form>

              {resolvedContent && (
                <div style={{ marginTop: '1em' }}>
                  {resolvedContent.error ? (
                    <Message error>
                      <p>{resolvedContent.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Resolved Successfully</Message.Header>
                      <p>
                        <strong>External ID:</strong> {resolvedContent.externalId}<br />
                        <strong>Content ID:</strong> {resolvedContent.contentId}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* ContentID Validation */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="check circle" />
                ContentID Validation
              </Card.Header>
              <Card.Description>
                Validate ContentID format and extract components
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentID to Validate</label>
                  <Input
                    placeholder="e.g., content:audio:track:mb-12345"
                    value={validateContentIdInput}
                    onChange={(e) => setValidateContentIdInput(e.target.value)}
                    action={
                      <Button
                        primary
                        loading={validating}
                        disabled={!validateContentIdInput.trim() || validating}
                        onClick={handleValidate}
                      >
                        Validate
                      </Button>
                    }
                  />
                </Form.Field>
              </Form>

              {validatedContent && (
                <div style={{ marginTop: '1em' }}>
                  {validatedContent.error ? (
                    <Message error>
                      <p>{validatedContent.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Valid ContentID</Message.Header>
                      <p>
                        <strong>Domain:</strong> {validatedContent.domain}<br />
                        <strong>Type:</strong> {validatedContent.type}<br />
                        <strong>ID:</strong> {validatedContent.id}<br />
                        <strong>Audio:</strong> {validatedContent.isAudio ? 'Yes' : 'No'} |
                        <strong>Video:</strong> {validatedContent.isVideo ? 'Yes' : 'No'} |
                        <strong>Image:</strong> {validatedContent.isImage ? 'Yes' : 'No'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Domain Search */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search plus" />
                Domain Search
              </Card.Header>
              <Card.Description>
                Find ContentIDs by domain and optional type
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Domain</label>
                    <Input
                      placeholder="e.g., audio, video, image"
                      value={domain}
                      onChange={(e) => setDomain(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Type (optional)</label>
                    <Input
                      placeholder="e.g., track, movie, photo"
                      value={type}
                      onChange={(e) => setType(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={searchingDomain}
                  disabled={!domain.trim() || searchingDomain}
                  onClick={handleDomainSearch}
                >
                  Search Domain
                </Button>
              </Form>

              {domainResults && (
                <div style={{ marginTop: '1em' }}>
                  {domainResults.error ? (
                    <Message error>
                      <p>{domainResults.error}</p>
                    </Message>
                  ) : (
                    <div>
                      <p><strong>Found {domainResults.contentIds?.length || 0} ContentIDs</strong></p>
                      {domainResults.contentIds?.length > 0 && (
                        <List divided relaxed style={{ maxHeight: '200px', overflow: 'auto' }}>
                          {domainResults.contentIds.map((id, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <code>{id}</code>
                              </List.Content>
                            </List.Item>
                          ))}
                        </List>
                      )}
                    </div>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Examples */}
        <Grid.Column width={16}>
          <Segment>
            <Header as="h3">
              <Icon name="lightbulb" />
              ContentID Examples
            </Header>
            <p>Click any example to fill the registration form:</p>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5em' }}>
              {Object.entries(contentExamples).map(([domainName, types]) =>
                Object.entries(types).map(([typeName, example]) => (
                  <Button
                    key={`${domainName}-${typeName}`}
                    size="small"
                    onClick={() => fillExample(domainName, typeName)}
                  >
                    {domainName}:{typeName}
                  </Button>
                ))
              )}
            </div>
          </Segment>
        </Grid.Column>

        {/* IPLD Graph Traversal */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="sitemap" />
                IPLD Graph Traversal
              </Card.Header>
              <Card.Description>
                Traverse content relationships following specific link types
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Start ContentID</label>
                    <Input
                      placeholder="e.g., content:audio:track:mb-12345"
                      value={traverseContentId}
                      onChange={(e) => setTraverseContentId(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Link Type</label>
                    <Input
                      placeholder="e.g., album, artist, artwork"
                      value={traverseLinkName}
                      onChange={(e) => setTraverseLinkName(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={traversing}
                  disabled={!traverseContentId.trim() || !traverseLinkName.trim() || traversing}
                  onClick={handleTraverse}
                >
                  Traverse Graph
                </Button>
              </Form>

              {traversalResults && (
                <div style={{ marginTop: '1em' }}>
                  {traversalResults.error ? (
                    <Message error>
                      <p>{traversalResults.error}</p>
                    </Message>
                  ) : (
                    <div>
                      <p><strong>Traversal completed:</strong> {traversalResults.completedTraversal ? 'Yes' : 'No'}</p>
                      <p><strong>Visited {traversalResults.visitedNodes?.length || 0} nodes</strong></p>
                      {traversalResults.visitedNodes?.length > 0 && (
                        <List divided relaxed style={{ maxHeight: '150px', overflow: 'auto' }}>
                          {traversalResults.visitedNodes.map((node, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header>{node.contentId}</List.Header>
                                <List.Description>
                                  {node.outgoingLinks?.length || 0} outgoing links
                                </List.Description>
                              </List.Content>
                            </List.Item>
                          ))}
                        </List>
                      )}
                    </div>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Content Graph */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="share alternate" />
                Content Graph
              </Card.Header>
              <Card.Description>
                Get the complete relationship graph for a ContentID
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentID</label>
                  <Input
                    placeholder="Enter ContentID to get its graph"
                    value={graphContentId}
                    onChange={(e) => setGraphContentId(e.target.value)}
                    action={
                      <Button
                        primary
                        loading={gettingGraph}
                        disabled={!graphContentId.trim() || gettingGraph}
                        onClick={handleGetGraph}
                      >
                        Get Graph
                      </Button>
                    }
                  />
                </Form.Field>
              </Form>

              {graphResults && (
                <div style={{ marginTop: '1em' }}>
                  {graphResults.error ? (
                    <Message error>
                      <p>{graphResults.error}</p>
                    </Message>
                  ) : (
                    <div>
                      <p><strong>Root:</strong> {graphResults.rootContentId}</p>
                      <p><strong>Nodes:</strong> {graphResults.nodes?.length || 0}</p>
                      <p><strong>Paths:</strong> {graphResults.paths?.length || 0}</p>
                      {graphResults.nodes?.length > 0 && (
                        <List divided relaxed style={{ maxHeight: '150px', overflow: 'auto' }}>
                          {graphResults.nodes.slice(0, 5).map((node, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header style={{ fontSize: '0.9em' }}>
                                  {node.contentId}
                                </List.Header>
                                <List.Description style={{ fontSize: '0.8em' }}>
                                  {node.outgoingLinks?.length || 0} outgoing, {node.incomingLinks?.length || 0} incoming
                                </List.Description>
                              </List.Content>
                            </List.Item>
                          ))}
                          {graphResults.nodes.length > 5 && (
                            <List.Item>
                              <List.Content>
                                <em>... and {graphResults.nodes.length - 5} more nodes</em>
                              </List.Content>
                            </List.Item>
                          )}
                        </List>
                      )}
                    </div>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Inbound Links */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="arrow left" />
                Inbound Links
              </Card.Header>
              <Card.Description>
                Find all content that links to a specific ContentID
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>Target ContentID</label>
                  <Input
                    placeholder="Find content that links to this ID"
                    value={inboundTargetId}
                    onChange={(e) => setInboundTargetId(e.target.value)}
                    action={
                      <Button
                        primary
                        loading={findingInbound}
                        disabled={!inboundTargetId.trim() || findingInbound}
                        onClick={handleFindInbound}
                      >
                        Find Links
                      </Button>
                    }
                  />
                </Form.Field>
              </Form>

              {inboundResults && (
                <div style={{ marginTop: '1em' }}>
                  {inboundResults.error ? (
                    <Message error>
                      <p>{inboundResults.error}</p>
                    </Message>
                  ) : (
                    <div>
                      <p><strong>Found {inboundResults.inboundLinks?.length || 0} inbound links</strong></p>
                      {inboundResults.inboundLinks?.length > 0 && (
                        <List divided relaxed style={{ maxHeight: '150px', overflow: 'auto' }}>
                          {inboundResults.inboundLinks.map((link, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <code style={{ fontSize: '0.9em' }}>{link}</code>
                              </List.Content>
                            </List.Item>
                          ))}
                        </List>
                      )}
                    </div>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Perceptual Hash - Audio */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="sound" />
                Audio Perceptual Hash
              </Card.Header>
              <Card.Description>
                Compute perceptual hash for audio similarity detection
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Algorithm</label>
                    <Dropdown
                      selection
                      options={supportedAlgorithms?.algorithms?.map(alg => ({ key: alg, text: alg, value: alg })) || []}
                      value={audioAlgorithm}
                      onChange={(e, { value }) => setAudioAlgorithm(value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Sample Rate (Hz)</label>
                    <Input
                      type="number"
                      value={sampleRate}
                      onChange={(e) => setSampleRate(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Audio Samples (comma-separated floats)</label>
                  <TextArea
                    placeholder="0.1, -0.2, 0.3, ... (normalized -1.0 to 1.0)"
                    value={audioSamples}
                    onChange={(e) => setAudioSamples(e.target.value)}
                    rows={3}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={computingAudioHash}
                  disabled={!audioSamples.trim() || computingAudioHash}
                  onClick={handleComputeAudioHash}
                >
                  Compute Audio Hash
                </Button>
              </Form>

              {audioHashResult && (
                <div style={{ marginTop: '1em' }}>
                  {audioHashResult.error ? (
                    <Message error>
                      <p>{audioHashResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Audio Hash Computed</Message.Header>
                      <p>
                        <strong>Algorithm:</strong> {audioHashResult.algorithm}<br />
                        <strong>Hex Hash:</strong> {audioHashResult.hex}<br />
                        <strong>Sample Count:</strong> {audioSamples.split(',').filter(s => s.trim()).length}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Perceptual Hash - Image */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="image" />
                Image Perceptual Hash
              </Card.Header>
              <Card.Description>
                Compute perceptual hash for image similarity detection
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Algorithm</label>
                    <Dropdown
                      selection
                      options={supportedAlgorithms?.algorithms?.filter(alg => alg !== 'ChromaPrint').map(alg => ({ key: alg, text: alg, value: alg })) || []}
                      value={imageAlgorithm}
                      onChange={(e, { value }) => setImageAlgorithm(value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Dimensions</label>
                    <Input
                      placeholder="Width x Height"
                      value={`${imageWidth}x${imageHeight}`}
                      onChange={(e) => {
                        const [w, h] = e.target.value.split('x').map(s => parseInt(s.trim()));
                        if (!isNaN(w)) setImageWidth(w);
                        if (!isNaN(h)) setImageHeight(h);
                      }}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Pixel Data (comma-separated bytes 0-255)</label>
                  <TextArea
                    placeholder="255, 128, 64, ... (RGBA pixel data)"
                    value={imagePixels}
                    onChange={(e) => setImagePixels(e.target.value)}
                    rows={3}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={computingImageHash}
                  disabled={!imagePixels.trim() || computingImageHash}
                  onClick={handleComputeImageHash}
                >
                  Compute Image Hash
                </Button>
              </Form>

              {imageHashResult && (
                <div style={{ marginTop: '1em' }}>
                  {imageHashResult.error ? (
                    <Message error>
                      <p>{imageHashResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Image Hash Computed</Message.Header>
                      <p>
                        <strong>Algorithm:</strong> {imageHashResult.algorithm}<br />
                        <strong>Hex Hash:</strong> {imageHashResult.hex}<br />
                        <strong>Dimensions:</strong> {imageWidth}x{imageHeight}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Hash Similarity Analysis */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="balance scale" />
                Hash Similarity Analysis
              </Card.Header>
              <Card.Description>
                Compare perceptual hashes to determine content similarity
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Hash A (hex)</label>
                    <Input
                      placeholder="First hash value (hexadecimal)"
                      value={hashA}
                      onChange={(e) => setHashA(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Hash B (hex)</label>
                    <Input
                      placeholder="Second hash value (hexadecimal)"
                      value={hashB}
                      onChange={(e) => setHashB(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Similarity Threshold</label>
                    <Input
                      type="number"
                      min="0"
                      max="1"
                      step="0.1"
                      value={similarityThreshold}
                      onChange={(e) => setSimilarityThreshold(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={computingSimilarity}
                  disabled={!hashA.trim() || !hashB.trim() || computingSimilarity}
                  onClick={handleComputeSimilarity}
                >
                  Analyze Similarity
                </Button>
              </Form>

              {similarityResult && (
                <div style={{ marginTop: '1em' }}>
                  {similarityResult.error ? (
                    <Message error>
                      <p>{similarityResult.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Similarity Analysis Results</Message.Header>
                      <p>
                        <strong>Hamming Distance:</strong> {similarityResult.hammingDistance} bits<br />
                        <strong>Similarity Score:</strong> {(similarityResult.similarity * 100).toFixed(1)}%<br />
                        <strong>Are Similar:</strong> {similarityResult.areSimilar ? 'Yes' : 'No'} (threshold: {(similarityResult.threshold * 100).toFixed(1)}%)
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Fuzzy Content Matching */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="magic" />
                Fuzzy Content Matching
              </Card.Header>
              <Card.Description>
                Find similar content using perceptual hashes and text analysis
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>Target ContentID</label>
                  <Input
                    placeholder="ContentID to find matches for"
                    value={findSimilarContentId}
                    onChange={(e) => setFindSimilarContentId(e.target.value)}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Min Confidence</label>
                    <Input
                      type="number"
                      min="0"
                      max="1"
                      step="0.1"
                      value={findSimilarMinConfidence}
                      onChange={(e) => setFindSimilarMinConfidence(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Max Results</label>
                    <Input
                      type="number"
                      min="1"
                      max="50"
                      value={findSimilarMaxResults}
                      onChange={(e) => setFindSimilarMaxResults(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={findingSimilarContent}
                  disabled={!findSimilarContentId.trim() || findingSimilarContent}
                  onClick={handleFindSimilarContent}
                >
                  Find Similar Content
                </Button>
              </Form>

              {findSimilarResult && (
                <div style={{ marginTop: '1em' }}>
                  {findSimilarResult.error ? (
                    <Message error>
                      <p>{findSimilarResult.error}</p>
                    </Message>
                  ) : (
                    <div>
                      <p><strong>Target:</strong> {findSimilarResult.targetContentId}</p>
                      <p><strong>Searched {findSimilarResult.totalCandidates} candidates</strong></p>
                      <p><strong>Found {findSimilarResult.matches?.length || 0} matches</strong></p>
                      {findSimilarResult.matches?.length > 0 && (
                        <List divided relaxed style={{ maxHeight: '200px', overflow: 'auto' }}>
                          {findSimilarResult.matches.map((match, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header style={{ fontSize: '0.9em' }}>
                                  {match.candidateContentId}
                                </List.Header>
                                <List.Description>
                                  Confidence: {(match.confidence * 100).toFixed(1)}% |
                                  Reason: {match.reason}
                                </List.Description>
                              </List.Content>
                            </List.Item>
                          ))}
                        </List>
                      )}
                    </div>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Perceptual Similarity */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="chart bar" />
                Perceptual Similarity
              </Card.Header>
              <Card.Description>
                Compare perceptual similarity between two ContentIDs
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>ContentID A</label>
                    <Input
                      placeholder="First ContentID"
                      value={perceptualContentIdA}
                      onChange={(e) => setPerceptualContentIdA(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>ContentID B</label>
                    <Input
                      placeholder="Second ContentID"
                      value={perceptualContentIdB}
                      onChange={(e) => setPerceptualContentIdB(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Similarity Threshold</label>
                  <Input
                    type="number"
                    min="0"
                    max="1"
                    step="0.1"
                    value={perceptualThreshold}
                    onChange={(e) => setPerceptualThreshold(e.target.value)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={computingPerceptualSimilarity}
                  disabled={!perceptualContentIdA.trim() || !perceptualContentIdB.trim() || computingPerceptualSimilarity}
                  onClick={handleComputePerceptualSimilarity}
                >
                  Compute Similarity
                </Button>
              </Form>

              {perceptualSimilarityResult && (
                <div style={{ marginTop: '1em' }}>
                  {perceptualSimilarityResult.error ? (
                    <Message error>
                      <p>{perceptualSimilarityResult.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Similarity Analysis</Message.Header>
                      <p>
                        <strong>Content A:</strong> {perceptualSimilarityResult.contentIdA}<br />
                        <strong>Content B:</strong> {perceptualSimilarityResult.contentIdB}<br />
                        <strong>Similarity:</strong> {(perceptualSimilarityResult.similarity * 100).toFixed(1)}%<br />
                        <strong>Are Similar:</strong> {perceptualSimilarityResult.isSimilar ? 'Yes' : 'No'} (threshold: {(perceptualSimilarityResult.threshold * 100).toFixed(1)}%)
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Text Similarity */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="font" />
                Text Similarity Analysis
              </Card.Header>
              <Card.Description>
                Compare text strings using Levenshtein distance and phonetic matching
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Text A</label>
                    <Input
                      placeholder="First text string"
                      value={textSimilarityA}
                      onChange={(e) => setTextSimilarityA(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Text B</label>
                    <Input
                      placeholder="Second text string"
                      value={textSimilarityB}
                      onChange={(e) => setTextSimilarityB(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={computingTextSimilarity}
                  disabled={!textSimilarityA.trim() || !textSimilarityB.trim() || computingTextSimilarity}
                  onClick={handleComputeTextSimilarity}
                >
                  Analyze Text Similarity
                </Button>
              </Form>

              {textSimilarityResult && (
                <div style={{ marginTop: '1em' }}>
                  {textSimilarityResult.error ? (
                    <Message error>
                      <p>{textSimilarityResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Text Similarity Results</Message.Header>
                      <p>
                        <strong>Text A:</strong> "{textSimilarityResult.textA}"<br />
                        <strong>Text B:</strong> "{textSimilarityResult.textB}"<br />
                        <strong>Levenshtein Similarity:</strong> {(textSimilarityResult.levenshteinSimilarity * 100).toFixed(1)}%<br />
                        <strong>Phonetic Similarity:</strong> {(textSimilarityResult.phoneticSimilarity * 100).toFixed(1)}%<br />
                        <strong>Combined Similarity:</strong> {(textSimilarityResult.combinedSimilarity * 100).toFixed(1)}%
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Metadata Portability - Export */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="download" />
                Export Metadata
              </Card.Header>
              <Card.Description>
                Export metadata for ContentIDs to a portable package
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentIDs (one per line)</label>
                  <TextArea
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    value={exportContentIds}
                    onChange={(e) => setExportContentIds(e.target.value)}
                    rows={4}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    label="Include IPLD links"
                    checked={includeLinks}
                    onChange={(e, { checked }) => setIncludeLinks(checked)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={exportingMetadata}
                  disabled={!exportContentIds.trim() || exportingMetadata}
                  onClick={handleExportMetadata}
                >
                  Export Metadata
                </Button>
              </Form>

              {exportResult && (
                <div style={{ marginTop: '1em' }}>
                  {exportResult.error ? (
                    <Message error>
                      <p>{exportResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Export Successful</Message.Header>
                      <p>
                        <strong>Version:</strong> {exportResult.version}<br />
                        <strong>Entries:</strong> {exportResult.metadata?.totalEntries || 0}<br />
                        <strong>Links:</strong> {exportResult.metadata?.totalLinks || 0}<br />
                        <strong>Checksum:</strong> {exportResult.metadata?.checksum?.substring(0, 16)}...
                      </p>
                      <details>
                        <summary>View Package JSON</summary>
                        <pre style={{ fontSize: '0.8em', maxHeight: '200px', overflow: 'auto' }}>
                          {JSON.stringify(exportResult, null, 2)}
                        </pre>
                      </details>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Metadata Portability - Import */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="upload" />
                Import Metadata
              </Card.Header>
              <Card.Description>
                Import metadata from a portable package with conflict resolution
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>Conflict Resolution Strategy</label>
                  <Dropdown
                    selection
                    options={availableStrategies?.strategies?.map(s => ({
                      key: s.strategy,
                      text: s.name,
                      value: s.strategy,
                      description: s.description
                    })) || []}
                    value={conflictStrategy}
                    onChange={(e, { value }) => setConflictStrategy(value)}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    label="Dry run (preview changes without applying)"
                    checked={dryRun}
                    onChange={(e, { checked }) => setDryRun(checked)}
                  />
                </Form.Field>
                <Button
                  secondary
                  loading={analyzingConflicts}
                  disabled={!importPackage.trim() || analyzingConflicts}
                  onClick={handleAnalyzeConflicts}
                >
                  Analyze Conflicts
                </Button>
                <Button
                  primary
                  loading={importingMetadata}
                  disabled={!importPackage.trim() || importingMetadata}
                  onClick={handleImportMetadata}
                  style={{ marginLeft: '0.5em' }}
                >
                  Import Metadata
                </Button>
              </Form>

              {/* Import Package Input */}
              <Form style={{ marginTop: '1em' }}>
                <Form.Field>
                  <label>Metadata Package (JSON)</label>
                  <TextArea
                    placeholder="Paste exported metadata package JSON here..."
                    value={importPackage}
                    onChange={(e) => setImportPackage(e.target.value)}
                    rows={6}
                  />
                </Form.Field>
              </Form>

              {/* Results */}
              {conflictAnalysis && (
                <div style={{ marginTop: '1em' }}>
                  {conflictAnalysis.error ? (
                    <Message error>
                      <p>{conflictAnalysis.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Conflict Analysis</Message.Header>
                      <p>
                        <strong>Total Entries:</strong> {conflictAnalysis.totalEntries}<br />
                        <strong>Conflicting:</strong> {conflictAnalysis.conflictingEntries}<br />
                        <strong>Clean:</strong> {conflictAnalysis.cleanEntries}<br />
                        <strong>Recommended Strategy:</strong> {Object.entries(conflictAnalysis.recommendedStrategies || {})
                          .sort(([,a], [,b]) => b - a)[0]?.[0] || 'Merge'}
                      </p>
                    </Message>
                  )}
                </div>
              )}

              {importResult && (
                <div style={{ marginTop: '1em' }}>
                  {importResult.error ? (
                    <Message error>
                      <p>{importResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Import {importResult.success ? 'Successful' : 'Completed with Issues'}</Message.Header>
                      <p>
                        <strong>Processed:</strong> {importResult.entriesProcessed}<br />
                        <strong>Imported:</strong> {importResult.entriesImported}<br />
                        <strong>Skipped:</strong> {importResult.entriesSkipped}<br />
                        <strong>Conflicts Resolved:</strong> {importResult.conflictsResolved}<br />
                        <strong>Duration:</strong> {importResult.duration?.TotalSeconds.toFixed(2)}s
                      </p>
                      {importResult.errors?.length > 0 && (
                        <details>
                          <summary>Errors ({importResult.errors.length})</summary>
                          <List bulleted>
                            {importResult.errors.map((error, index) => (
                              <List.Item key={index}>{error}</List.Item>
                            ))}
                          </List>
                        </details>
                      )}
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Content Descriptor Publishing */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="cloud upload" />
                Publish Content Descriptor
              </Card.Header>
              <Card.Description>
                Publish a content descriptor to the DHT with versioning support
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentID</label>
                  <Input
                    placeholder="content:audio:track:mb-12345"
                    value={publishContentId}
                    onChange={(e) => setPublishContentId(e.target.value)}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Codec</label>
                    <Input
                      placeholder="mp3, flac, etc."
                      value={publishCodec}
                      onChange={(e) => setPublishCodec(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Size (bytes)</label>
                    <Input
                      type="number"
                      value={publishSize}
                      onChange={(e) => setPublishSize(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={publishingDescriptor}
                  disabled={!publishContentId.trim() || publishingDescriptor}
                  onClick={handlePublishDescriptor}
                >
                  Publish Descriptor
                </Button>
              </Form>

              {publishResult && (
                <div style={{ marginTop: '1em' }}>
                  {publishResult.error ? (
                    <Message error>
                      <p>{publishResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Published Successfully</Message.Header>
                      <p>
                        <strong>ContentID:</strong> {publishResult.contentId}<br />
                        <strong>Version:</strong> {publishResult.version}<br />
                        <strong>TTL:</strong> {publishResult.ttl?.totalMinutes} minutes<br />
                        <strong>Was Updated:</strong> {publishResult.wasUpdated ? 'Yes' : 'No'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Batch Publishing */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="list" />
                Batch Publish Descriptors
              </Card.Header>
              <Card.Description>
                Publish multiple content descriptors simultaneously
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentIDs (one per line)</label>
                  <TextArea
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    value={batchContentIds}
                    onChange={(e) => setBatchContentIds(e.target.value)}
                    rows={6}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={publishingBatch}
                  disabled={!batchContentIds.trim() || publishingBatch}
                  onClick={handlePublishBatch}
                >
                  Publish Batch
                </Button>
              </Form>

              {batchPublishResult && (
                <div style={{ marginTop: '1em' }}>
                  {batchPublishResult.error ? (
                    <Message error>
                      <p>{batchPublishResult.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Batch Publish Results</Message.Header>
                      <p>
                        <strong>Total Requested:</strong> {batchPublishResult.totalRequested}<br />
                        <strong>Successfully Published:</strong> {batchPublishResult.successfullyPublished}<br />
                        <strong>Failed:</strong> {batchPublishResult.failedToPublish}<br />
                        <strong>Skipped:</strong> {batchPublishResult.skipped}<br />
                        <strong>Duration:</strong> {batchPublishResult.totalDuration?.totalSeconds.toFixed(2)}s
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Descriptor Updates */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="edit" />
                Update Descriptor
              </Card.Header>
              <Card.Description>
                Update metadata for an existing published descriptor
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>Target ContentID</label>
                  <Input
                    placeholder="ContentID to update"
                    value={updateTargetId}
                    onChange={(e) => setUpdateTargetId(e.target.value)}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>New Codec</label>
                    <Input
                      placeholder="Leave empty to keep current"
                      value={updateCodec}
                      onChange={(e) => setUpdateCodec(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>New Size (bytes)</label>
                    <Input
                      placeholder="Leave empty to keep current"
                      value={updateSize}
                      onChange={(e) => setUpdateSize(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>New Confidence (0.0-1.0)</label>
                  <Input
                    placeholder="Leave empty to keep current"
                    value={updateConfidence}
                    onChange={(e) => setUpdateConfidence(e.target.value)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={updatingDescriptor}
                  disabled={!updateTargetId.trim() || updatingDescriptor}
                  onClick={handleUpdateDescriptor}
                >
                  Update Descriptor
                </Button>
              </Form>

              {updateResult && (
                <div style={{ marginTop: '1em' }}>
                  {updateResult.error ? (
                    <Message error>
                      <p>{updateResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Update Successful</Message.Header>
                      <p>
                        <strong>ContentID:</strong> {updateResult.contentId}<br />
                        <strong>Version:</strong> {updateResult.previousVersion} → {updateResult.newVersion}<br />
                        <strong>Updates Applied:</strong> {updateResult.appliedUpdates?.join(', ') || 'none'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Publishing Management */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="cogs" />
                Publishing Management
              </Card.Header>
              <Card.Description>
                Manage published descriptors and monitor publishing status
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button.Group fluid>
                <Button
                  loading={republishing}
                  disabled={republishing}
                  onClick={handleRepublishExpiring}
                >
                  Republish Expiring
                </Button>
                <Button
                  loading={loadingStats}
                  disabled={loadingStats}
                  onClick={handleLoadPublishingStats}
                >
                  Load Stats
                </Button>
              </Button.Group>

              {/* Republish Results */}
              {republishResult && (
                <div style={{ marginTop: '1em' }}>
                  {republishResult.error ? (
                    <Message error>
                      <p>{republishResult.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Republish Results</Message.Header>
                      <p>
                        <strong>Checked:</strong> {republishResult.totalChecked}<br />
                        <strong>Republished:</strong> {republishResult.republished}<br />
                        <strong>Failed:</strong> {republishResult.failed}<br />
                        <strong>Still Valid:</strong> {republishResult.stillValid}<br />
                        <strong>Duration:</strong> {republishResult.duration?.totalSeconds.toFixed(2)}s
                      </p>
                    </Message>
                  )}
                </div>
              )}

              {/* Publishing Stats */}
              {publishingStats && (
                <div style={{ marginTop: '1em' }}>
                  {publishingStats.error ? (
                    <Message error>
                      <p>{publishingStats.error}</p>
                    </Message>
                  ) : (
                    <Message>
                      <Message.Header>Publishing Statistics</Message.Header>
                      <p>
                        <strong>Total Published:</strong> {publishingStats.totalPublishedDescriptors}<br />
                        <strong>Active Publications:</strong> {publishingStats.activePublications}<br />
                        <strong>Expiring Soon:</strong> {publishingStats.expiringSoon}<br />
                        <strong>Average TTL:</strong> {publishingStats.averageTtlHours?.toFixed(1)} hours<br />
                        <strong>Total Storage:</strong> {(publishingStats.totalStorageBytes / 1024 / 1024)?.toFixed(1)} MB
                      </p>
                      {publishingStats.publicationsByDomain && Object.keys(publishingStats.publicationsByDomain).length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>By Domain:</strong>
                          {Object.entries(publishingStats.publicationsByDomain).map(([domain, count]) => (
                            <Label key={domain} size="tiny" style={{ margin: '0.1em' }}>
                              {domain}: {count}
                            </Label>
                          ))}
                        </div>
                      )}
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Descriptor Retrieval */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search" />
                Retrieve Content Descriptor
              </Card.Header>
              <Card.Description>
                Retrieve content descriptors from the DHT by ContentID
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentID</label>
                  <Input
                    placeholder="content:audio:track:mb-12345"
                    value={retrieveContentId}
                    onChange={(e) => setRetrieveContentId(e.target.value)}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    label="Bypass cache (force fresh retrieval)"
                    checked={bypassCache}
                    onChange={(e, { checked }) => setBypassCache(checked)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={retrievingDescriptor}
                  disabled={!retrieveContentId.trim() || retrievingDescriptor}
                  onClick={handleRetrieveDescriptor}
                >
                  Retrieve Descriptor
                </Button>
              </Form>

              {retrievalResult && (
                <div style={{ marginTop: '1em' }}>
                  {retrievalResult.error ? (
                    <Message error>
                      <p>{retrievalResult.error}</p>
                    </Message>
                  ) : !retrievalResult.found ? (
                    <Message warning>
                      <p>Content descriptor not found for: {retrievalResult.contentId || retrieveContentId}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Descriptor Retrieved</Message.Header>
                      <p>
                        <strong>ContentID:</strong> {retrievalResult.descriptor?.contentId}<br />
                        <strong>From Cache:</strong> {retrievalResult.fromCache ? 'Yes' : 'No'}<br />
                        <strong>Retrieved:</strong> {new Date(retrievalResult.retrievedAt).toLocaleString()}<br />
                        <strong>Duration:</strong> {retrievalResult.retrievalDuration?.totalMilliseconds.toFixed(0)}ms<br />
                        <strong>Verified:</strong> {retrievalResult.verification?.isValid ? 'Yes' : 'No'}
                        {retrievalResult.verification?.warnings?.length > 0 && (
                          <span> (with warnings)</span>
                        )}
                      </p>
                      <details>
                        <summary>View Descriptor JSON</summary>
                        <pre style={{ fontSize: '0.8em', maxHeight: '200px', overflow: 'auto' }}>
                          {JSON.stringify(retrievalResult.descriptor, null, 2)}
                        </pre>
                      </details>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Batch Retrieval */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="list alternate" />
                Batch Descriptor Retrieval
              </Card.Header>
              <Card.Description>
                Retrieve multiple content descriptors simultaneously
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>ContentIDs (one per line)</label>
                  <TextArea
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    value={batchRetrieveContentIds}
                    onChange={(e) => setBatchRetrieveContentIds(e.target.value)}
                    rows={6}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={retrievingBatch}
                  disabled={!batchRetrieveContentIds.trim() || retrievingBatch}
                  onClick={handleRetrieveBatch}
                >
                  Retrieve Batch
                </Button>
              </Form>

              {batchRetrievalResult && (
                <div style={{ marginTop: '1em' }}>
                  {batchRetrievalResult.error ? (
                    <Message error>
                      <p>{batchRetrievalResult.error}</p>
                    </Message>
                  ) : (
                    <Message info>
                      <Message.Header>Batch Retrieval Results</Message.Header>
                      <p>
                        <strong>Requested:</strong> {batchRetrievalResult.requested}<br />
                        <strong>Found:</strong> {batchRetrievalResult.found}<br />
                        <strong>Failed:</strong> {batchRetrievalResult.failed}<br />
                        <strong>Duration:</strong> {batchRetrievalResult.totalDuration?.totalSeconds.toFixed(2)}s
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Domain Query */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="filter" />
                Query by Domain
              </Card.Header>
              <Card.Description>
                Query content descriptors by domain and optional type
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Domain</label>
                    <Dropdown
                      selection
                      options={[
                        { key: 'audio', text: 'Audio', value: 'audio' },
                        { key: 'video', text: 'Video', value: 'video' },
                        { key: 'image', text: 'Image', value: 'image' },
                        { key: 'text', text: 'Text', value: 'text' },
                        { key: 'application', text: 'Application', value: 'application' }
                      ]}
                      value={queryDomain}
                      onChange={(e, { value }) => setQueryDomain(value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Type (optional)</label>
                    <Input
                      placeholder="track, album, movie, etc."
                      value={queryType}
                      onChange={(e) => setQueryType(e.target.value)}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Max Results</label>
                    <Input
                      type="number"
                      min="1"
                      max="1000"
                      value={queryMaxResults}
                      onChange={(e) => setQueryMaxResults(e.target.value)}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  primary
                  loading={queryingDescriptors}
                  disabled={!queryDomain.trim() || queryingDescriptors}
                  onClick={handleQueryDescriptors}
                >
                  Query Domain
                </Button>
              </Form>

              {queryResult && (
                <div style={{ marginTop: '1em' }}>
                  {queryResult.error ? (
                    <Message error>
                      <p>{queryResult.error}</p>
                    </Message>
                  ) : (
                    <Message>
                      <Message.Header>Query Results</Message.Header>
                      <p>
                        <strong>Domain:</strong> {queryResult.domain}
                        {queryResult.type && <span> | <strong>Type:</strong> {queryResult.type}</span>}<br />
                        <strong>Found:</strong> {queryResult.totalFound}<br />
                        <strong>Query Time:</strong> {queryResult.queryDuration?.totalMilliseconds.toFixed(0)}ms<br />
                        <strong>Has More:</strong> {queryResult.hasMoreResults ? 'Yes' : 'No'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Descriptor Verification */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="shield" />
                Descriptor Verification
              </Card.Header>
              <Card.Description>
                Verify descriptor signature and freshness
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Field>
                  <label>Descriptor JSON</label>
                  <TextArea
                    placeholder="Paste descriptor JSON to verify..."
                    value={verifyDescriptor}
                    onChange={(e) => setVerifyDescriptor(e.target.value)}
                    rows={8}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={verifyingDescriptor}
                  disabled={!verifyDescriptor.trim() || verifyingDescriptor}
                  onClick={handleVerifyDescriptor}
                >
                  Verify Descriptor
                </Button>
              </Form>

              {verificationResult && (
                <div style={{ marginTop: '1em' }}>
                  {verificationResult.error ? (
                    <Message error>
                      <p>{verificationResult.error}</p>
                    </Message>
                  ) : (
                    <Message success={verificationResult.isValid} warning={!verificationResult.isValid}>
                      <Message.Header>
                        Verification Result: {verificationResult.isValid ? 'Valid' : 'Invalid'}
                      </Message.Header>
                      <p>
                        <strong>Signature Valid:</strong> {verificationResult.signatureValid ? 'Yes' : 'No'}<br />
                        <strong>Freshness Valid:</strong> {verificationResult.freshnessValid ? 'Yes' : 'No'}<br />
                        <strong>Age:</strong> {verificationResult.age?.totalMinutes.toFixed(1)} minutes
                      </p>
                      {verificationResult.warnings?.length > 0 && (
                        <div>
                          <strong>Warnings:</strong>
                          <List bulleted>
                            {verificationResult.warnings.map((warning, index) => (
                              <List.Item key={index}>{warning}</List.Item>
                            ))}
                          </List>
                        </div>
                      )}
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Retrieval Management */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="chart line" />
                Retrieval Management
              </Card.Header>
              <Card.Description>
                Monitor retrieval performance and manage cache
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button.Group fluid>
                <Button
                  loading={loadingRetrievalStats}
                  disabled={loadingRetrievalStats}
                  onClick={handleLoadRetrievalStats}
                >
                  Load Stats
                </Button>
                <Button
                  onClick={handleClearRetrievalCache}
                >
                  Clear Cache
                </Button>
              </Button.Group>

              {/* Retrieval Stats */}
              {retrievalStats && (
                <div style={{ marginTop: '1em' }}>
                  {retrievalStats.error ? (
                    <Message error>
                      <p>{retrievalStats.error}</p>
                    </Message>
                  ) : (
                    <Message>
                      <Message.Header>Retrieval Statistics</Message.Header>
                      <p>
                        <strong>Total Retrievals:</strong> {retrievalStats.totalRetrievals}<br />
                        <strong>Cache Hits:</strong> {retrievalStats.cacheHits}<br />
                        <strong>Cache Misses:</strong> {retrievalStats.cacheMisses}<br />
                        <strong>Hit Ratio:</strong> {(retrievalStats.cacheHitRatio * 100).toFixed(1)}%<br />
                        <strong>Avg Retrieval Time:</strong> {retrievalStats.averageRetrievalTime?.totalMilliseconds.toFixed(0)}ms<br />
                        <strong>Active Cache Entries:</strong> {retrievalStats.activeCacheEntries}<br />
                        <strong>Cache Size:</strong> {(retrievalStats.cacheSizeBytes / 1024).toFixed(1)} KB
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* MediaCore Statistics Dashboard */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="chart bar" />
                MediaCore Statistics Dashboard
              </Card.Header>
              <Card.Description>
                Comprehensive overview of all MediaCore system performance and usage metrics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button.Group fluid>
                <Button
                  primary
                  loading={loadingDashboard}
                  disabled={loadingDashboard}
                  onClick={handleLoadMediaCoreDashboard}
                >
                  Load Full Dashboard
                </Button>
                <Button
                  color="red"
                  onClick={handleResetMediaCoreStats}
                >
                  Reset All Stats
                </Button>
              </Button.Group>

              {/* Dashboard Overview */}
              {mediaCoreDashboard && !mediaCoreDashboard.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message info>
                    <Message.Header>System Overview</Message.Header>
                    <p>
                      <strong>Uptime:</strong> {mediaCoreDashboard.uptime ? `${Math.floor(mediaCoreDashboard.uptime.totalHours)}h ${mediaCoreDashboard.uptime.minutes}m` : 'N/A'}<br />
                      <strong>Last Updated:</strong> {mediaCoreDashboard.timestamp ? new Date(mediaCoreDashboard.timestamp).toLocaleString() : 'N/A'}
                    </p>
                  </Message>

                  {/* System Resources */}
                  {mediaCoreDashboard.systemResources && (
                    <Message>
                      <Message.Header>System Resources</Message.Header>
                      <p>
                        <strong>Working Set:</strong> {(mediaCoreDashboard.systemResources.workingSetBytes / 1024 / 1024).toFixed(1)} MB<br />
                        <strong>Private Memory:</strong> {(mediaCoreDashboard.systemResources.privateMemoryBytes / 1024 / 1024).toFixed(1)} MB<br />
                        <strong>GC Memory:</strong> {(mediaCoreDashboard.systemResources.gcTotalMemoryBytes / 1024 / 1024).toFixed(1)} MB<br />
                        <strong>Thread Count:</strong> {mediaCoreDashboard.systemResources.threadCount}
                      </p>
                    </Message>
                  )}
                </div>
              )}

              {/* Error Display */}
              {mediaCoreDashboard?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>Failed to load dashboard: {mediaCoreDashboard.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Content Registry Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="database" />
                Content Registry
              </Card.Header>
              <Card.Description>
                Content ID mappings and domain statistics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingRegistryStats}
                disabled={loadingRegistryStats}
                onClick={handleLoadContentRegistryStats}
              >
                Load Registry Stats
              </Button>

              {contentRegistryStats && !contentRegistryStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message success>
                    <Message.Header>Registry Overview</Message.Header>
                    <p>
                      <strong>Total Mappings:</strong> {contentRegistryStats.totalMappings}<br />
                      <strong>Domains:</strong> {contentRegistryStats.totalDomains}<br />
                      <strong>Avg Mappings/Domain:</strong> {contentRegistryStats.averageMappingsPerDomain.toFixed(1)}
                    </p>
                    {contentRegistryStats.mappingsByDomain && Object.keys(contentRegistryStats.mappingsByDomain).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Mappings by Domain:</strong>
                        {Object.entries(contentRegistryStats.mappingsByDomain).map(([domain, count]) => (
                          <Label key={domain} size="tiny" style={{ margin: '0.1em' }}>
                            {domain}: {count}
                          </Label>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {contentRegistryStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{contentRegistryStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Descriptor Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search" />
                Descriptor Retrieval
              </Card.Header>
              <Card.Description>
                Cache performance and retrieval statistics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingDescriptorStats}
                disabled={loadingDescriptorStats}
                onClick={handleLoadDescriptorStats}
              >
                Load Descriptor Stats
              </Button>

              {descriptorStats && !descriptorStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Cache Performance</Message.Header>
                    <p>
                      <strong>Total Retrievals:</strong> {descriptorStats.totalRetrievals}<br />
                      <strong>Cache Hits:</strong> {descriptorStats.cacheHits}<br />
                      <strong>Cache Misses:</strong> {descriptorStats.cacheMisses}<br />
                      <strong>Hit Ratio:</strong> {(descriptorStats.cacheHitRatio * 100).toFixed(1)}%<br />
                      <strong>Avg Retrieval Time:</strong> {descriptorStats.averageRetrievalTime?.totalMilliseconds.toFixed(0)}ms<br />
                      <strong>Active Cache Entries:</strong> {descriptorStats.activeCacheEntries}<br />
                      <strong>Cache Size:</strong> {(descriptorStats.cacheSizeBytes / 1024).toFixed(1)} KB
                    </p>
                  </Message>
                </div>
              )}

              {descriptorStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{descriptorStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Fuzzy Matching Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="magic" />
                Fuzzy Matching
              </Card.Header>
              <Card.Description>
                Similarity detection and accuracy metrics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingFuzzyStats}
                disabled={loadingFuzzyStats}
                onClick={handleLoadFuzzyMatchingStats}
              >
                Load Fuzzy Stats
              </Button>

              {fuzzyMatchingStats && !fuzzyMatchingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Matching Performance</Message.Header>
                    <p>
                      <strong>Total Matches:</strong> {fuzzyMatchingStats.totalMatches}<br />
                      <strong>Success Rate:</strong> {(fuzzyMatchingStats.successRate * 100).toFixed(1)}%<br />
                      <strong>Avg Confidence:</strong> {(fuzzyMatchingStats.averageConfidenceScore * 100).toFixed(1)}%<br />
                      <strong>Avg Match Time:</strong> {fuzzyMatchingStats.averageMatchingTime?.totalMilliseconds.toFixed(0)}ms
                    </p>
                    {fuzzyMatchingStats.accuracyByAlgorithm && Object.keys(fuzzyMatchingStats.accuracyByAlgorithm).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Algorithm Accuracy:</strong>
                        {Object.entries(fuzzyMatchingStats.accuracyByAlgorithm).map(([algorithm, stats]) => (
                          <div key={algorithm} style={{ margin: '0.2em 0' }}>
                            <small>{algorithm}: F1={stats.f1Score.toFixed(2)}, Precision={stats.precision.toFixed(2)}</small>
                          </div>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {fuzzyMatchingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{fuzzyMatchingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Perceptual Hashing Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="hashtag" />
                Perceptual Hashing
              </Card.Header>
              <Card.Description>
                Hash computation performance and accuracy
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingPerceptualStats}
                disabled={loadingPerceptualStats}
                onClick={handleLoadPerceptualHashingStats}
              >
                Load Hashing Stats
              </Button>

              {perceptualHashingStats && !perceptualHashingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Hashing Performance</Message.Header>
                    <p>
                      <strong>Total Hashes:</strong> {perceptualHashingStats.totalHashesComputed}<br />
                      <strong>Avg Computation Time:</strong> {perceptualHashingStats.averageComputationTime?.totalMilliseconds.toFixed(0)}ms<br />
                      <strong>Overall Accuracy:</strong> {(perceptualHashingStats.overallAccuracy * 100).toFixed(1)}%<br />
                      <strong>Duplicates Detected:</strong> {perceptualHashingStats.duplicateHashesDetected}
                    </p>
                    {perceptualHashingStats.statsByAlgorithm && Object.keys(perceptualHashingStats.statsByAlgorithm).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Algorithm Breakdown:</strong>
                        {Object.entries(perceptualHashingStats.statsByAlgorithm).map(([algorithm, stats]) => (
                          <div key={algorithm} style={{ margin: '0.2em 0' }}>
                            <small>{algorithm}: {stats.hashesComputed} hashes, {stats.averageTime.totalMilliseconds.toFixed(0)}ms avg, {stats.accuracy.toFixed(2)} accuracy</small>
                          </div>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {perceptualHashingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{perceptualHashingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* IPLD Mapping Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="sitemap" />
                IPLD Mapping
              </Card.Header>
              <Card.Description>
                Graph structure and link statistics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingIpldStats}
                disabled={loadingIpldStats}
                onClick={handleLoadIpldMappingStats}
              >
                Load IPLD Stats
              </Button>

              {ipldMappingStats && !ipldMappingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Graph Statistics</Message.Header>
                    <p>
                      <strong>Total Links:</strong> {ipldMappingStats.totalLinks}<br />
                      <strong>Total Nodes:</strong> {ipldMappingStats.totalNodes}<br />
                      <strong>Total Graphs:</strong> {ipldMappingStats.totalGraphs}<br />
                      <strong>Connectivity Ratio:</strong> {(ipldMappingStats.graphConnectivityRatio * 100).toFixed(1)}%<br />
                      <strong>Broken Links:</strong> {ipldMappingStats.brokenLinksDetected}<br />
                      <strong>Avg Traversal Time:</strong> {ipldMappingStats.averageTraversalTime?.totalMilliseconds.toFixed(0)}ms
                    </p>
                  </Message>
                </div>
              )}

              {ipldMappingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{ipldMappingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Metadata Portability Stats */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="exchange" />
                Metadata Portability
              </Card.Header>
              <Card.Description>
                Export/import operations and conflict resolution
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingPortabilityStats}
                disabled={loadingPortabilityStats}
                onClick={handleLoadMetadataPortabilityStats}
              >
                Load Portability Stats
              </Button>

              {metadataPortabilityStats && !metadataPortabilityStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Portability Metrics</Message.Header>
                    <p>
                      <strong>Total Exports:</strong> {metadataPortabilityStats.totalExports}<br />
                      <strong>Total Imports:</strong> {metadataPortabilityStats.totalImports}<br />
                      <strong>Import Success Rate:</strong> {(metadataPortabilityStats.importSuccessRate * 100).toFixed(1)}%<br />
                      <strong>Data Transferred:</strong> {(metadataPortabilityStats.totalDataTransferred / 1024).toFixed(1)} KB<br />
                      <strong>Avg Export Time:</strong> {metadataPortabilityStats.averageExportTime?.totalMilliseconds.toFixed(0)}ms<br />
                      <strong>Avg Import Time:</strong> {metadataPortabilityStats.averageImportTime?.totalMilliseconds.toFixed(0)}ms
                    </p>
                  </Message>
                </div>
              )}

              {metadataPortabilityStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{metadataPortabilityStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Content Publishing Stats */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="cloud upload" />
                Content Publishing
              </Card.Header>
              <Card.Description>
                DHT publishing performance and publication management
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button
                fluid
                loading={loadingPublishingStats}
                disabled={loadingPublishingStats}
                onClick={handleLoadContentPublishingStats}
              >
                Load Publishing Stats
              </Button>

              {contentPublishingStats && !contentPublishingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Publishing Overview</Message.Header>
                    <p>
                      <strong>Total Published:</strong> {contentPublishingStats.totalPublished}<br />
                      <strong>Active Publications:</strong> {contentPublishingStats.activePublications}<br />
                      <strong>Expired Publications:</strong> {contentPublishingStats.expiredPublications}<br />
                      <strong>Success Rate:</strong> {(contentPublishingStats.publicationSuccessRate * 100).toFixed(1)}%<br />
                      <strong>Republished:</strong> {contentPublishingStats.republishedDescriptors}<br />
                      <strong>Failed:</strong> {contentPublishingStats.failedPublications}<br />
                      <strong>Avg Publish Time:</strong> {contentPublishingStats.averagePublishTime?.totalMilliseconds.toFixed(0)}ms
                    </p>
                    {contentPublishingStats.publicationsByDomain && Object.keys(contentPublishingStats.publicationsByDomain).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Publications by Domain:</strong>
                        {Object.entries(contentPublishingStats.publicationsByDomain).map(([domain, count]) => (
                          <Label key={domain} size="tiny" style={{ margin: '0.1em' }}>
                            {domain}: {count}
                          </Label>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {contentPublishingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>{contentPublishingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* PodCore DHT Publishing */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="broadcast" />
                PodCore DHT Publishing
              </Card.Header>
              <Card.Description>
                Publish and manage pod metadata on the decentralized DHT for discovery
              </Card.Description>
            </Card.Content>

            {/* Publish Pod */}
            <Card.Content>
              <Header size="small">Publish Pod to DHT</Header>
              <Form>
                <Form.TextArea
                  label="Pod JSON"
                  placeholder='{"id": {"value": "pod:artist:mb:daft-punk-hash"}, "displayName": "Daft Punk Fans", "visibility": "Listed", "focusType": "ContentId", "focusContentId": {"domain": "audio", "type": "artist", "id": "daft-punk-hash"}, "tags": ["electronic", "french-house"], "createdAt": "2024-01-01T00:00:00Z", "createdBy": "alice", "metadata": {"description": "A community for Daft Punk fans", "memberCount": 150}}'
                  value={podToPublish}
                  onChange={(e) => setPodToPublish(e.target.value)}
                  rows={6}
                />
                <Button
                  primary
                  loading={publishingPod}
                  disabled={publishingPod || !podToPublish.trim()}
                  onClick={handlePublishPod}
                >
                  Publish Pod
                </Button>
              </Form>

              {podPublishingResult && (
                <div style={{ marginTop: '1em' }}>
                  {podPublishingResult.error ? (
                    <Message error>
                      <p>Failed to publish pod: {podPublishingResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Pod Published Successfully</Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {podPublishingResult.podId?.value || podPublishingResult.podId}<br />
                        <strong>DHT Key:</strong> {podPublishingResult.dhtKey}<br />
                        <strong>Published:</strong> {new Date(podPublishingResult.publishedAt).toLocaleString()}<br />
                        <strong>Expires:</strong> {new Date(podPublishingResult.expiresAt).toLocaleString()}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Retrieve Pod Metadata */}
                  <Header size="small">Retrieve Pod Metadata</Header>
                  <Form>
                    <Form.Input
                      label="Pod ID"
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={podMetadataToRetrieve}
                      onChange={(e) => setPodMetadataToRetrieve(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={retrievingPodMetadata}
                      disabled={retrievingPodMetadata || !podMetadataToRetrieve.trim()}
                      onClick={handleRetrievePodMetadata}
                    >
                      Retrieve Metadata
                    </Button>
                  </Form>

                  {podMetadataResult && (
                    <div style={{ marginTop: '1em' }}>
                      {podMetadataResult.error ? (
                        <Message error>
                          <p>Failed to retrieve metadata: {podMetadataResult.error}</p>
                        </Message>
                      ) : podMetadataResult.found ? (
                        <Message success>
                          <Message.Header>Pod Metadata Retrieved</Message.Header>
                          <p>
                            <strong>Pod ID:</strong> {podMetadataResult.podId?.value || podMetadataResult.podId}<br />
                            <strong>Signature Valid:</strong> {podMetadataResult.isValidSignature ? 'Yes' : 'No'}<br />
                            <strong>Retrieved:</strong> {new Date(podMetadataResult.retrievedAt).toLocaleString()}<br />
                            <strong>Expires:</strong> {new Date(podMetadataResult.expiresAt).toLocaleString()}<br />
                            <strong>Display Name:</strong> {podMetadataResult.publishedPod?.displayName}<br />
                            <strong>Members:</strong> {podMetadataResult.publishedPod?.metadata?.memberCount || 'Unknown'}
                          </p>
                        </Message>
                      ) : (
                        <Message warning>
                          <p>Pod not found in DHT</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Unpublish Pod */}
                  <Header size="small">Unpublish Pod from DHT</Header>
                  <Form>
                    <Form.Input
                      label="Pod ID"
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={podToUnpublish}
                      onChange={(e) => setPodToUnpublish(e.target.value)}
                    />
                    <Button
                      fluid
                      color="red"
                      loading={unpublishingPod}
                      disabled={unpublishingPod || !podToUnpublish.trim()}
                      onClick={handleUnpublishPod}
                    >
                      Unpublish Pod
                    </Button>
                  </Form>

                  {podUnpublishResult && (
                    <div style={{ marginTop: '1em' }}>
                      {podUnpublishResult.error ? (
                        <Message error>
                          <p>Failed to unpublish pod: {podUnpublishResult.error}</p>
                        </Message>
                      ) : (
                        <Message success>
                          <p>Pod unpublished successfully from DHT</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>

            {/* Pod Publishing Statistics */}
            <Card.Content>
              <Button.Group fluid>
                <Button
                  primary
                  loading={loadingPodStats}
                  disabled={loadingPodStats}
                  onClick={handleLoadPodPublishingStats}
                >
                  Load Pod Publishing Stats
                </Button>
              </Button.Group>

              {podPublishingStats && !podPublishingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Pod Publishing Statistics</Message.Header>
                    <p>
                      <strong>Total Published:</strong> {podPublishingStats.totalPublished}<br />
                      <strong>Active Publications:</strong> {podPublishingStats.activePublications}<br />
                      <strong>Expired Publications:</strong> {podPublishingStats.expiredPublications}<br />
                      <strong>Failed Publications:</strong> {podPublishingStats.failedPublications}<br />
                      <strong>Avg Publish Time:</strong> {podPublishingStats.averagePublishTime ? `${podPublishingStats.averagePublishTime.totalMilliseconds.toFixed(0)}ms` : 'N/A'}<br />
                      <strong>Last Operation:</strong> {podPublishingStats.lastPublishOperation ? new Date(podPublishingStats.lastPublishOperation).toLocaleString() : 'Never'}
                    </p>
                    {podPublishingStats.publicationsByVisibility && Object.keys(podPublishingStats.publicationsByVisibility).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Publications by Visibility:</strong>
                        {Object.entries(podPublishingStats.publicationsByVisibility).map(([visibility, count]) => (
                          <Label key={visibility} size="tiny" style={{ margin: '0.1em' }}>
                            {visibility}: {count}
                          </Label>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {podPublishingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>Failed to load pod publishing stats: {podPublishingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Membership Management */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="users" />
                Pod Membership Management
              </Card.Header>
              <Card.Description>
                Manage signed membership records in DHT with role-based access control
              </Card.Description>
            </Card.Content>

            {/* Publish Membership */}
            <Card.Content>
              <Header size="small">Publish Membership Record</Header>
              <Form>
                <Form.TextArea
                  label="Membership Record JSON"
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "role": "member", "isBanned": false, "publicKey": "base64-ed25519-key", "joinedAt": "2024-01-01T00:00:00Z"}'
                  value={membershipRecord}
                  onChange={(e) => setMembershipRecord(e.target.value)}
                  rows={4}
                />
                <Button
                  primary
                  loading={publishingMembership}
                  disabled={publishingMembership || !membershipRecord.trim()}
                  onClick={handlePublishMembership}
                >
                  Publish Membership
                </Button>
              </Form>

              {membershipPublishResult && (
                <div style={{ marginTop: '1em' }}>
                  {membershipPublishResult.error ? (
                    <Message error>
                      <p>Failed to publish membership: {membershipPublishResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Membership Published Successfully</Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {membershipPublishResult.podId}<br />
                        <strong>Peer ID:</strong> {membershipPublishResult.peerId}<br />
                        <strong>DHT Key:</strong> {membershipPublishResult.dhtKey}<br />
                        <strong>Published:</strong> {new Date(membershipPublishResult.publishedAt).toLocaleString()}<br />
                        <strong>Expires:</strong> {new Date(membershipPublishResult.expiresAt).toLocaleString()}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Get Membership */}
                  <Header size="small">Get Membership Record</Header>
                  <Form>
                    <Form.Input
                      label="Pod ID"
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={membershipPodId}
                      onChange={(e) => setMembershipPodId(e.target.value)}
                    />
                    <Form.Input
                      label="Peer ID"
                      placeholder="alice"
                      value={membershipPeerId}
                      onChange={(e) => setMembershipPeerId(e.target.value)}
                    />
                    <Button.Group fluid>
                      <Button
                        loading={gettingMembership}
                        disabled={gettingMembership || !membershipPodId.trim() || !membershipPeerId.trim()}
                        onClick={handleGetMembership}
                      >
                        Get Membership
                      </Button>
                      <Button
                        loading={verifyingMembership}
                        disabled={verifyingMembership || !membershipPodId.trim() || !membershipPeerId.trim()}
                        onClick={handleVerifyMembership}
                      >
                        Verify Membership
                      </Button>
                    </Button.Group>
                  </Form>

                  {/* Membership Results */}
                  {membershipResult && (
                    <div style={{ marginTop: '1em' }}>
                      {membershipResult.error ? (
                        <Message error>
                          <p>Failed to get membership: {membershipResult.error}</p>
                        </Message>
                      ) : membershipResult.found ? (
                        <Message success>
                          <Message.Header>Membership Found</Message.Header>
                          <p>
                            <strong>Pod ID:</strong> {membershipResult.podId}<br />
                            <strong>Peer ID:</strong> {membershipResult.peerId}<br />
                            <strong>Role:</strong> {membershipResult.signedRecord?.membership?.role}<br />
                            <strong>Banned:</strong> {membershipResult.signedRecord?.membership?.isBanned ? 'Yes' : 'No'}<br />
                            <strong>Signature Valid:</strong> {membershipResult.isValidSignature ? 'Yes' : 'No'}<br />
                            <strong>Joined:</strong> {membershipResult.signedRecord?.membership?.joinedAt ? new Date(membershipResult.signedRecord.membership.joinedAt).toLocaleString() : 'Unknown'}
                          </p>
                        </Message>
                      ) : (
                        <Message warning>
                          <p>Membership not found in DHT</p>
                        </Message>
                      )}
                    </div>
                  )}

                  {/* Verification Results */}
                  {membershipVerification && (
                    <div style={{ marginTop: '1em' }}>
                      {membershipVerification.error ? (
                        <Message error>
                          <p>Failed to verify membership: {membershipVerification.error}</p>
                        </Message>
                      ) : (
                        <Message info>
                          <Message.Header>Membership Verification</Message.Header>
                          <p>
                            <strong>Valid Member:</strong> {membershipVerification.isValidMember ? 'Yes' : 'No'}<br />
                            <strong>Role:</strong> {membershipVerification.role || 'None'}<br />
                            <strong>Banned:</strong> {membershipVerification.isBanned ? 'Yes' : 'No'}
                          </p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Member Management */}
                  <Header size="small">Member Management</Header>

                  {/* Ban Member */}
                  <Form style={{ marginBottom: '1em' }}>
                    <Form.Input
                      label="Ban Reason (optional)"
                      placeholder="Violation of community rules"
                      value={banReason}
                      onChange={(e) => setBanReason(e.target.value)}
                    />
                    <Button
                      fluid
                      color="red"
                      loading={banningMember}
                      disabled={banningMember || !membershipPodId.trim() || !membershipPeerId.trim()}
                      onClick={handleBanMember}
                    >
                      Ban Member
                    </Button>
                  </Form>

                  {/* Change Role */}
                  <Form>
                    <Form.Select
                      label="New Role"
                      options={[
                        { key: 'member', text: 'Member', value: 'member' },
                        { key: 'mod', text: 'Moderator', value: 'mod' },
                        { key: 'owner', text: 'Owner', value: 'owner' }
                      ]}
                      value={newRole}
                      onChange={(e, { value }) => setNewRole(value)}
                    />
                    <Button
                      fluid
                      color="blue"
                      loading={changingRole}
                      disabled={changingRole || !membershipPodId.trim() || !membershipPeerId.trim()}
                      onClick={handleChangeRole}
                    >
                      Change Role
                    </Button>
                  </Form>

                  {/* Management Results */}
                  {banResult && (
                    <Message success style={{ marginTop: '1em' }}>
                      <p>Member banned successfully</p>
                    </Message>
                  )}

                  {roleChangeResult && (
                    <Message success style={{ marginTop: '1em' }}>
                      <p>Member role changed successfully</p>
                    </Message>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>

            {/* Membership Statistics */}
            <Card.Content>
              <Button.Group fluid>
                <Button
                  primary
                  loading={loadingMembershipStats}
                  disabled={loadingMembershipStats}
                  onClick={handleLoadMembershipStats}
                >
                  Load Membership Stats
                </Button>
                <Button
                  color="orange"
                  onClick={handleCleanupMemberships}
                >
                  Cleanup Expired
                </Button>
              </Button.Group>

              {membershipStats && !membershipStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Membership Statistics</Message.Header>
                    <p>
                      <strong>Total Memberships:</strong> {membershipStats.totalMemberships}<br />
                      <strong>Active Memberships:</strong> {membershipStats.activeMemberships}<br />
                      <strong>Banned Memberships:</strong> {membershipStats.bannedMemberships}<br />
                      <strong>Expired Memberships:</strong> {membershipStats.expiredMemberships}<br />
                      <strong>Last Operation:</strong> {membershipStats.lastOperation ? new Date(membershipStats.lastOperation).toLocaleString() : 'Never'}
                    </p>
                    {membershipStats.membershipsByRole && Object.keys(membershipStats.membershipsByRole).length > 0 && (
                      <div style={{ marginTop: '0.5em' }}>
                        <strong>Memberships by Role:</strong>
                        {Object.entries(membershipStats.membershipsByRole).map(([role, count]) => (
                          <Label key={role} size="tiny" style={{ margin: '0.1em' }}>
                            {role}: {count}
                          </Label>
                        ))}
                      </div>
                    )}
                  </Message>
                </div>
              )}

              {membershipStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>Failed to load membership stats: {membershipStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Membership Verification */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="shield" />
                Pod Membership Verification
              </Card.Header>
              <Card.Description>
                Verify membership status, message authenticity, and role permissions for pod security
              </Card.Description>
            </Card.Content>

            {/* Membership Verification */}
            <Card.Content>
              <Header size="small">Verify Membership Status</Header>
              <Form>
                <Form.Group widths="equal">
                  <Form.Input
                    label="Pod ID"
                    placeholder="pod:artist:mb:daft-punk-hash"
                    value={verifyPodId}
                    onChange={(e) => setVerifyPodId(e.target.value)}
                  />
                  <Form.Input
                    label="Peer ID"
                    placeholder="alice"
                    value={verifyPeerId}
                    onChange={(e) => setVerifyPeerId(e.target.value)}
                  />
                </Form.Group>
                <Button
                  fluid
                  loading={verifyingMembership}
                  disabled={verifyingMembership || !verifyPodId.trim() || !verifyPeerId.trim()}
                  onClick={handleVerifyMembership}
                >
                  Verify Membership
                </Button>
              </Form>

              {membershipVerificationResult && (
                <div style={{ marginTop: '1em' }}>
                  {membershipVerificationResult.error ? (
                    <Message error>
                      <p>Failed to verify membership: {membershipVerificationResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Membership Verification Result</Message.Header>
                      <p>
                        <strong>Valid Member:</strong> {membershipVerificationResult.isValidMember ? 'Yes' : 'No'}<br />
                        <strong>Role:</strong> {membershipVerificationResult.role || 'None'}<br />
                        <strong>Banned:</strong> {membershipVerificationResult.isBanned ? 'Yes' : 'No'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Message Verification */}
                  <Header size="small">Verify Message Authenticity</Header>
                  <Form>
                    <Form.TextArea
                      label="Pod Message JSON"
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello everyone!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      value={messageToVerify}
                      onChange={(e) => setMessageToVerify(e.target.value)}
                      rows={4}
                    />
                    <Button
                      fluid
                      loading={verifyingMessage}
                      disabled={verifyingMessage || !messageToVerify.trim()}
                      onClick={handleVerifyMessage}
                    >
                      Verify Message
                    </Button>
                  </Form>

                  {messageVerificationResult && (
                    <div style={{ marginTop: '1em' }}>
                      {messageVerificationResult.error ? (
                        <Message error>
                          <p>Failed to verify message: {messageVerificationResult.error}</p>
                        </Message>
                      ) : (
                        <Message info>
                          <Message.Header>Message Verification Result</Message.Header>
                          <p>
                            <strong>Valid:</strong> {messageVerificationResult.isValid ? 'Yes' : 'No'}<br />
                            <strong>From Valid Member:</strong> {messageVerificationResult.isFromValidMember ? 'Yes' : 'No'}<br />
                            <strong>Not Banned:</strong> {messageVerificationResult.isNotBanned ? 'Yes' : 'No'}<br />
                            <strong>Valid Signature:</strong> {messageVerificationResult.hasValidSignature ? 'Yes' : 'No'}
                          </p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Role Checking */}
                  <Header size="small">Check Role Permissions</Header>
                  <Form>
                    <Form.Group widths="equal">
                      <Form.Input
                        label="Pod ID"
                        placeholder="pod:artist:mb:daft-punk-hash"
                        value={roleCheckPodId}
                        onChange={(e) => setRoleCheckPodId(e.target.value)}
                      />
                      <Form.Input
                        label="Peer ID"
                        placeholder="alice"
                        value={roleCheckPeerId}
                        onChange={(e) => setRoleCheckPeerId(e.target.value)}
                      />
                    </Form.Group>
                    <Form.Select
                      label="Required Role"
                      options={[
                        { key: 'member', text: 'Member', value: 'member' },
                        { key: 'mod', text: 'Moderator', value: 'mod' },
                        { key: 'owner', text: 'Owner', value: 'owner' }
                      ]}
                      value={requiredRole}
                      onChange={(e, { value }) => setRequiredRole(value)}
                    />
                    <Button
                      fluid
                      loading={checkingRole}
                      disabled={checkingRole || !roleCheckPodId.trim() || !roleCheckPeerId.trim()}
                      onClick={handleCheckRole}
                    >
                      Check Role
                    </Button>
                  </Form>

                  {roleCheckResult && (
                    <div style={{ marginTop: '1em' }}>
                      {roleCheckResult.error ? (
                        <Message error>
                          <p>Failed to check role: {roleCheckResult.error}</p>
                        </Message>
                      ) : (
                        <Message>
                          <Message.Header>Role Check Result</Message.Header>
                          <p>
                            <strong>Has Required Role ({requiredRole}):</strong> {roleCheckResult.hasRole ? 'Yes' : 'No'}
                          </p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>

            {/* Verification Statistics */}
            <Card.Content>
              <Button.Group fluid>
                <Button
                  primary
                  loading={loadingVerificationStats}
                  disabled={loadingVerificationStats}
                  onClick={handleLoadVerificationStats}
                >
                  Load Verification Stats
                </Button>
              </Button.Group>

              {verificationStats && !verificationStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Verification Statistics</Message.Header>
                    <p>
                      <strong>Total Verifications:</strong> {verificationStats.totalVerifications}<br />
                      <strong>Successful:</strong> {verificationStats.successfulVerifications}<br />
                      <strong>Failed Membership:</strong> {verificationStats.failedMembershipChecks}<br />
                      <strong>Failed Signatures:</strong> {verificationStats.failedSignatureChecks}<br />
                      <strong>Banned Rejections:</strong> {verificationStats.bannedMemberRejections}<br />
                      <strong>Avg Time:</strong> {verificationStats.averageVerificationTimeMs.toFixed(2)}ms<br />
                      <strong>Last Verification:</strong> {verificationStats.lastVerification ? new Date(verificationStats.lastVerification).toLocaleString() : 'Never'}
                    </p>
                  </Message>
                </div>
              )}

              {verificationStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>Failed to load verification stats: {verificationStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Discovery */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="search" />
                Pod Discovery
              </Card.Header>
              <Card.Description>
                Discover pods via DHT using name slugs, tags, and content associations
              </Card.Description>
            </Card.Content>

            {/* Pod Registration */}
            <Card.Content>
              <Header size="small">Register Pod for Discovery</Header>
              <Form>
                <Form.TextArea
                  label="Pod JSON (must have Visibility: Listed)"
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "name": "Daft Punk Fans", "visibility": "Listed", "focusContentId": "content:audio:artist:daft-punk", "tags": ["electronic", "french-house"]}'
                  value={podToRegister}
                  onChange={(e) => setPodToRegister(e.target.value)}
                  rows={3}
                />
                <Button
                  primary
                  loading={registeringPod}
                  disabled={registeringPod || !podToRegister.trim()}
                  onClick={handleRegisterPodForDiscovery}
                >
                  Register Pod
                </Button>
              </Form>

              {podRegistrationResult && (
                <div style={{ marginTop: '1em' }}>
                  {podRegistrationResult.error ? (
                    <Message error>
                      <p>Failed to register pod: {podRegistrationResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Pod Registered for Discovery</Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {podRegistrationResult.podId}<br />
                        <strong>Discovery Keys:</strong> {podRegistrationResult.discoveryKeys?.join(', ')}<br />
                        <strong>Registered:</strong> {new Date(podRegistrationResult.registeredAt).toLocaleString()}<br />
                        <strong>Expires:</strong> {new Date(podRegistrationResult.expiresAt).toLocaleString()}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Header size="small">Unregister Pod from Discovery</Header>
              <Form>
                <Form.Input
                  label="Pod ID"
                  placeholder="pod:artist:mb:daft-punk-hash"
                  value={podToUnregister}
                  onChange={(e) => setPodToUnregister(e.target.value)}
                />
                <Button
                  color="red"
                  loading={unregisteringPod}
                  disabled={unregisteringPod || !podToUnregister.trim()}
                  onClick={handleUnregisterPodFromDiscovery}
                >
                  Unregister Pod
                </Button>
              </Form>

              {podUnregistrationResult && (
                <div style={{ marginTop: '1em' }}>
                  {podUnregistrationResult.error ? (
                    <Message error>
                      <p>Failed to unregister pod: {podUnregistrationResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <p>Pod unregistered from discovery successfully</p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={4}>
                  {/* Discover by Name */}
                  <Header size="small">By Name</Header>
                  <Form>
                    <Form.Input
                      placeholder="daft-punk-fans"
                      value={discoverByName}
                      onChange={(e) => setDiscoverByName(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={discoveringByName}
                      disabled={discoveringByName || !discoverByName.trim()}
                      onClick={handleDiscoverByName}
                    >
                      Discover
                    </Button>
                  </Form>

                  {nameDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {nameDiscoveryResult.error ? (
                        <Message error size="tiny">
                          <p>{nameDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Found {nameDiscoveryResult.totalFound} pods</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={4}>
                  {/* Discover by Tag */}
                  <Header size="small">By Tag</Header>
                  <Form>
                    <Form.Input
                      placeholder="electronic"
                      value={discoverByTag}
                      onChange={(e) => setDiscoverByTag(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={discoveringByTag}
                      disabled={discoveringByTag || !discoverByTag.trim()}
                      onClick={handleDiscoverByTag}
                    >
                      Discover
                    </Button>
                  </Form>

                  {tagDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {tagDiscoveryResult.error ? (
                        <Message error size="tiny">
                          <p>{tagDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Found {tagDiscoveryResult.totalFound} pods</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={4}>
                  {/* Discover by Tags */}
                  <Header size="small">By Tags (AND)</Header>
                  <Form>
                    <Form.Input
                      placeholder="electronic,french-house"
                      value={discoverTags}
                      onChange={(e) => setDiscoverTags(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={discoveringByTags}
                      disabled={discoveringByTags || !discoverTags.trim()}
                      onClick={handleDiscoverByTags}
                    >
                      Discover
                    </Button>
                  </Form>

                  {tagsDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {tagsDiscoveryResult.error ? (
                        <Message error size="tiny">
                          <p>{tagsDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Found {tagsDiscoveryResult.totalFound} pods</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={4}>
                  {/* Discover All */}
                  <Header size="small">All Pods</Header>
                  <Form>
                    <Form.Input
                      label="Limit"
                      type="number"
                      min="1"
                      max="1000"
                      value={discoverLimit}
                      onChange={(e) => setDiscoverLimit(parseInt(e.target.value) || 50)}
                    />
                    <Button
                      fluid
                      loading={discoveringAll}
                      disabled={discoveringAll}
                      onClick={handleDiscoverAll}
                    >
                      Discover
                    </Button>
                  </Form>

                  {allDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {allDiscoveryResult.error ? (
                        <Message error size="tiny">
                          <p>{allDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Found {allDiscoveryResult.totalFound} pods</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Discover by Content */}
                  <Header size="small">By Content ID</Header>
                  <Form>
                    <Form.Input
                      placeholder="content:audio:artist:daft-punk"
                      value={discoverByContent}
                      onChange={(e) => setDiscoverByContent(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={discoveringByContent}
                      disabled={discoveringByContent || !discoverByContent.trim()}
                      onClick={handleDiscoverByContent}
                    >
                      Discover
                    </Button>
                  </Form>

                  {contentDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {contentDiscoveryResult.error ? (
                        <Message error size="tiny">
                          <p>{contentDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Found {contentDiscoveryResult.totalFound} pods</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Discovery Stats */}
                  <Header size="small">Discovery Statistics</Header>
                  <Button.Group fluid>
                    <Button
                      loading={loadingDiscoveryStats}
                      disabled={loadingDiscoveryStats}
                      onClick={handleLoadDiscoveryStats}
                    >
                      Load Stats
                    </Button>
                    <Button
                      color="blue"
                      onClick={handleRefreshDiscovery}
                    >
                      Refresh
                    </Button>
                  </Button.Group>

                  {discoveryStats && !discoveryStats.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <p>
                          <strong>Registered Pods:</strong> {discoveryStats.totalRegisteredPods}<br />
                          <strong>Active Entries:</strong> {discoveryStats.activeDiscoveryEntries}<br />
                          <strong>Expired Entries:</strong> {discoveryStats.expiredEntries}<br />
                          <strong>Avg Search Time:</strong> {discoveryStats.averageDiscoveryTime?.totalMilliseconds.toFixed(0)}ms
                        </p>
                      </Message>
                    </div>
                  )}

                  {discoveryStats?.error && (
                    <Message error size="tiny" style={{ marginTop: '0.5em' }}>
                      <p>{discoveryStats.error}</p>
                    </Message>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Supported Algorithms Info */}
        {supportedAlgorithms && (
          <Grid.Column width={16}>
            <Segment>
              <Header as="h3">
                <Icon name="cogs" />
                Supported Hash Algorithms
              </Header>
              <List divided relaxed>
                {supportedAlgorithms.algorithms.map(alg => (
                  <List.Item key={alg}>
                    <List.Content>
                      <List.Header>{alg}</List.Header>
                      <List.Description>
                        {supportedAlgorithms.descriptions[alg]}
                      </List.Description>
                    </List.Content>
                  </List.Item>
                ))}
              </List>
            </Segment>
          </Grid.Column>
        )}
      </Grid>
    </div>
  );
};

export default MediaCore;
