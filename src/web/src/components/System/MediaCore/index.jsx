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
  const [descriptorContentId, setDescriptorContentId] = useState('');
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
  const [descriptorVerificationResult, setDescriptorVerificationResult] = useState(null);
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
  const [verifyingMembershipStatus, setVerifyingMembershipStatus] = useState(false);
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
  const [membershipMessageToVerify, setMembershipMessageToVerify] = useState('');
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

  // Pod Join/Leave states
  const [joinRequestData, setJoinRequestData] = useState('');
  const [requestingJoin, setRequestingJoin] = useState(false);
  const [joinRequestResult, setJoinRequestResult] = useState(null);
  const [acceptanceData, setAcceptanceData] = useState('');
  const [acceptingJoin, setAcceptingJoin] = useState(false);
  const [acceptanceResult, setAcceptanceResult] = useState(null);
  const [leaveRequestData, setLeaveRequestData] = useState('');
  const [requestingLeave, setRequestingLeave] = useState(false);
  const [leaveRequestResult, setLeaveRequestResult] = useState(null);
  const [acceptingLeave, setAcceptingLeave] = useState(false);
  const [leaveAcceptanceResult, setLeaveAcceptanceResult] = useState(null);
  const [pendingPodId, setPendingPodId] = useState('');
  const [loadingPendingRequests, setLoadingPendingRequests] = useState(false);
  const [pendingJoinRequests, setPendingJoinRequests] = useState(null);
  const [pendingLeaveRequests, setPendingLeaveRequests] = useState(null);

  // Pod Message Routing states
  const [routeMessageData, setRouteMessageData] = useState('');
  const [routingMessage, setRoutingMessage] = useState(false);
  const [routingResult, setRoutingResult] = useState(null);
  const [routeToPeersMessage, setRouteToPeersMessage] = useState('');
  const [routeToPeersIds, setRouteToPeersIds] = useState('');
  const [routingToPeers, setRoutingToPeers] = useState(false);
  const [routingToPeersResult, setRoutingToPeersResult] = useState(null);
  const [routingStats, setRoutingStats] = useState(null);
  const [loadingRoutingStats, setLoadingRoutingStats] = useState(false);
  const [checkMessageId, setCheckMessageId] = useState('');
  const [checkPodId, setCheckPodId] = useState('');
  const [checkingMessageSeen, setCheckingMessageSeen] = useState(false);
  const [messageSeenResult, setMessageSeenResult] = useState(null);

  // Pod Message Signing states
  const [messageToSign, setMessageToSign] = useState('');
  const [privateKeyForSigning, setPrivateKeyForSigning] = useState('');
  const [signingMessage, setSigningMessage] = useState(false);
  const [signedMessageResult, setSignedMessageResult] = useState(null);
  const [messageToVerify, setMessageToVerify] = useState('');
  const [verifyingSignature, setVerifyingSignature] = useState(false);
  const [verificationResult, setVerificationResult] = useState(null);
  const [generatingKeyPair, setGeneratingKeyPair] = useState(false);
  const [generatedKeyPair, setGeneratedKeyPair] = useState(null);
  const [signingStats, setSigningStats] = useState(null);
  const [loadingSigningStats, setLoadingSigningStats] = useState(false);

  // Pod Message Storage states
  const [storageStats, setStorageStats] = useState(null);
  const [storageStatsLoading, setStorageStatsLoading] = useState(false);
  const [cleanupLoading, setCleanupLoading] = useState(false);
  const [rebuildIndexLoading, setRebuildIndexLoading] = useState(false);
  const [vacuumLoading, setVacuumLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState(null);
  const [searchLoading, setSearchLoading] = useState(false);

  // Pod Message Backfill states
  const [backfillStats, setBackfillStats] = useState(null);
  const [backfillStatsLoading, setBackfillStatsLoading] = useState(false);
  const [syncBackfillLoading, setSyncBackfillLoading] = useState(false);
  const [lastSeenTimestamps, setLastSeenTimestamps] = useState(null);
  const [backfillPodId, setBackfillPodId] = useState('');

  // Pod Channel Management states
  const [channels, setChannels] = useState([]);
  const [channelsLoading, setChannelsLoading] = useState(false);
  const [createChannelLoading, setCreateChannelLoading] = useState(false);
  const [updateChannelLoading, setUpdateChannelLoading] = useState(false);
  const [deleteChannelLoading, setDeleteChannelLoading] = useState(false);
  const [channelPodId, setChannelPodId] = useState('');
  const [newChannelName, setNewChannelName] = useState('');
  const [newChannelKind, setNewChannelKind] = useState('General');
  const [editingChannel, setEditingChannel] = useState(null);
  const [editChannelName, setEditChannelName] = useState('');

  // Pod Content Linking states
  const [contentId, setContentId] = useState('');
  const [contentValidation, setContentValidation] = useState(null);
  const [contentMetadata, setContentMetadata] = useState(null);
  const [contentSearchQuery, setContentSearchQuery] = useState('');
  const [contentSearchResults, setContentSearchResults] = useState([]);
  const [contentValidationLoading, setContentValidationLoading] = useState(false);
  const [contentMetadataLoading, setContentMetadataLoading] = useState(false);
  const [contentSearchLoading, setContentSearchLoading] = useState(false);
  const [createPodLoading, setCreatePodLoading] = useState(false);
  const [newPodName, setNewPodName] = useState('');
  const [newPodVisibility, setNewPodVisibility] = useState('Unlisted');

  // Pod Opinion Management states
  const [opinionPodId, setOpinionPodId] = useState('');
  const [opinionContentId, setOpinionContentId] = useState('');
  const [opinionVariantHash, setOpinionVariantHash] = useState('');
  const [opinionScore, setOpinionScore] = useState(5);
  const [opinionNote, setOpinionNote] = useState('');
  const [opinions, setOpinions] = useState([]);
  const [opinionStatistics, setOpinionStatistics] = useState(null);
  const [publishOpinionLoading, setPublishOpinionLoading] = useState(false);
  const [getOpinionsLoading, setGetOpinionsLoading] = useState(false);
  const [getStatsLoading, setGetStatsLoading] = useState(false);
  const [refreshOpinionsLoading, setRefreshOpinionsLoading] = useState(false);

  // Pod Opinion Aggregation states
  const [aggregatedOpinions, setAggregatedOpinions] = useState(null);
  const [memberAffinities, setMemberAffinities] = useState({});
  const [consensusRecommendations, setConsensusRecommendations] = useState([]);
  const [getAggregatedLoading, setGetAggregatedLoading] = useState(false);
  const [getAffinitiesLoading, setGetAffinitiesLoading] = useState(false);
  const [getRecommendationsLoading, setGetRecommendationsLoading] = useState(false);
  const [updateAffinitiesLoading, setUpdateAffinitiesLoading] = useState(false);
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
    if (!externalId.trim() || !descriptorContentId.trim()) return;

    try {
      setRegistering(true);
      await mediacore.registerContentId(externalId.trim(), descriptorContentId.trim());
      setExternalId('');
      setDescriptorContentId('');
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

      let packageData;
      try {
        packageData = JSON.parse(importPackage.trim());
      } catch (parseErr) {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.importMetadata(packageData, conflictStrategy, dryRun);
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

      let packageData;
      try {
        packageData = JSON.parse(importPackage.trim());
      } catch (parseErr) {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.analyzeMetadataConflicts(packageData);
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
    if (!membershipMessageToVerify.trim()) {
      alert('Please enter a message JSON');
      return;
    }

    try {
      setVerifyingMessage(true);
      setMessageVerificationResult(null);
      const message = JSON.parse(membershipMessageToVerify);
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

  // Pod Join/Leave handlers
  const handleRequestJoin = async () => {
    if (!joinRequestData.trim()) {
      alert('Please enter join request JSON data');
      return;
    }

    try {
      setRequestingJoin(true);
      setJoinRequestResult(null);
      const joinRequest = JSON.parse(joinRequestData);
      const result = await mediacore.requestPodJoin(joinRequest);
      setJoinRequestResult(result);
      setJoinRequestData('');
    } catch (err) {
      setJoinRequestResult({ error: err.message });
    } finally {
      setRequestingJoin(false);
    }
  };

  const handleAcceptJoin = async () => {
    if (!acceptanceData.trim()) {
      alert('Please enter acceptance JSON data');
      return;
    }

    try {
      setAcceptingJoin(true);
      setAcceptanceResult(null);
      const acceptance = JSON.parse(acceptanceData);
      const result = await mediacore.acceptPodJoin(acceptance);
      setAcceptanceResult(result);
      setAcceptanceData('');
    } catch (err) {
      setAcceptanceResult({ error: err.message });
    } finally {
      setAcceptingJoin(false);
    }
  };

  const handleRequestLeave = async () => {
    if (!leaveRequestData.trim()) {
      alert('Please enter leave request JSON data');
      return;
    }

    try {
      setRequestingLeave(true);
      setLeaveRequestResult(null);
      const leaveRequest = JSON.parse(leaveRequestData);
      const result = await mediacore.requestPodLeave(leaveRequest);
      setLeaveRequestResult(result);
      setLeaveRequestData('');
    } catch (err) {
      setLeaveRequestResult({ error: err.message });
    } finally {
      setRequestingLeave(false);
    }
  };

  const handleAcceptLeave = async () => {
    if (!acceptanceData.trim()) {
      alert('Please enter leave acceptance JSON data');
      return;
    }

    try {
      setAcceptingLeave(true);
      setLeaveAcceptanceResult(null);
      const acceptance = JSON.parse(acceptanceData);
      const result = await mediacore.acceptPodLeave(acceptance);
      setLeaveAcceptanceResult(result);
      setAcceptanceData('');
    } catch (err) {
      setLeaveAcceptanceResult({ error: err.message });
    } finally {
      setAcceptingLeave(false);
    }
  };

  const handleLoadPendingRequests = async () => {
    if (!pendingPodId.trim()) {
      alert('Please enter a pod ID');
      return;
    }

    try {
      setLoadingPendingRequests(true);
      setPendingJoinRequests(null);
      setPendingLeaveRequests(null);

      const [joinRequests, leaveRequests] = await Promise.all([
        mediacore.getPendingJoinRequests(pendingPodId),
        mediacore.getPendingLeaveRequests(pendingPodId)
      ]);

      setPendingJoinRequests(joinRequests);
      setPendingLeaveRequests(leaveRequests);
    } catch (err) {
      setPendingJoinRequests({ error: err.message });
      setPendingLeaveRequests({ error: err.message });
    } finally {
      setLoadingPendingRequests(false);
    }
  };

  // Pod Message Routing handlers
  const handleRouteMessage = async () => {
    if (!routeMessageData.trim()) {
      alert('Please enter message JSON data');
      return;
    }

    try {
      setRoutingMessage(true);
      setRoutingResult(null);
      const message = JSON.parse(routeMessageData);
      const result = await mediacore.routePodMessage(message);
      setRoutingResult(result);
      setRouteMessageData('');
    } catch (err) {
      setRoutingResult({ error: err.message });
    } finally {
      setRoutingMessage(false);
    }
  };

  const handleRouteMessageToPeers = async () => {
    if (!routeToPeersMessage.trim() || !routeToPeersIds.trim()) {
      alert('Please enter message JSON and target peer IDs');
      return;
    }

    try {
      setRoutingToPeers(true);
      setRoutingToPeersResult(null);
      const message = JSON.parse(routeToPeersMessage);
      const targetPeerIds = routeToPeersIds.split(',').map(id => id.trim()).filter(id => id);
      const result = await mediacore.routePodMessageToPeers(message, targetPeerIds);
      setRoutingToPeersResult(result);
      setRouteToPeersMessage('');
      setRouteToPeersIds('');
    } catch (err) {
      setRoutingToPeersResult({ error: err.message });
    } finally {
      setRoutingToPeers(false);
    }
  };

  const handleLoadRoutingStats = async () => {
    try {
      setLoadingRoutingStats(true);
      setRoutingStats(null);
      const result = await mediacore.getPodMessageRoutingStats();
      setRoutingStats(result);
    } catch (err) {
      setRoutingStats({ error: err.message });
    } finally {
      setLoadingRoutingStats(false);
    }
  };

  const handleCheckMessageSeen = async () => {
    if (!checkMessageId.trim() || !checkPodId.trim()) {
      alert('Please enter both message ID and pod ID');
      return;
    }

    try {
      setCheckingMessageSeen(true);
      setMessageSeenResult(null);
      const result = await mediacore.checkMessageSeen(checkMessageId, checkPodId);
      setMessageSeenResult(result);
    } catch (err) {
      setMessageSeenResult({ error: err.message });
    } finally {
      setCheckingMessageSeen(false);
    }
  };

  const handleRegisterMessageSeen = async () => {
    if (!checkMessageId.trim() || !checkPodId.trim()) {
      alert('Please enter both message ID and pod ID');
      return;
    }

    try {
      const result = await mediacore.registerMessageSeen(checkMessageId, checkPodId);
      alert(`Message registered as seen: ${result.wasNewlyRegistered ? 'New' : 'Already known'}`);
    } catch (err) {
      alert(`Failed to register message: ${err.message}`);
    }
  };

  const handleCleanupSeenMessages = async () => {
    try {
      const result = await mediacore.cleanupSeenMessages();
      alert(`Cleanup completed: ${result.messagesCleaned} messages cleaned, ${result.messagesRetained} retained`);
      // Reload stats to reflect changes
      await handleLoadRoutingStats();
    } catch (err) {
      alert(`Failed to cleanup: ${err.message}`);
    }
  };

  // Pod Message Signing handlers
  const handleSignMessage = async () => {
    if (!messageToSign.trim() || !privateKeyForSigning.trim()) {
      alert('Please enter message JSON and private key');
      return;
    }

    try {
      setSigningMessage(true);
      setSignedMessageResult(null);
      const message = JSON.parse(messageToSign);
      const result = await mediacore.signPodMessage(message, privateKeyForSigning);
      setSignedMessageResult(result);
      setMessageToSign('');
    } catch (err) {
      setSignedMessageResult({ error: err.message });
    } finally {
      setSigningMessage(false);
    }
  };

  const handleVerifySignature = async () => {
    if (!messageToVerify.trim()) {
      alert('Please enter message JSON to verify');
      return;
    }

    try {
      setVerifyingSignature(true);
      setVerificationResult(null);
      const message = JSON.parse(messageToVerify);
      const result = await mediacore.verifyPodMessageSignature(message);
      setVerificationResult(result);
    } catch (err) {
      setVerificationResult({ error: err.message });
    } finally {
      setVerifyingSignature(false);
    }
  };

  const handleGenerateKeyPair = async () => {
    try {
      setGeneratingKeyPair(true);
      setGeneratedKeyPair(null);
      const result = await mediacore.generateMessageKeyPair();
      setGeneratedKeyPair(result);
    } catch (err) {
      setGeneratedKeyPair({ error: err.message });
    } finally {
      setGeneratingKeyPair(false);
    }
  };

  const handleLoadSigningStats = async () => {
    try {
      setLoadingSigningStats(true);
      setSigningStats(null);
      const result = await mediacore.getMessageSigningStats();
      setSigningStats(result);
    } catch (err) {
      setSigningStats({ error: err.message });
    } finally {
      setLoadingSigningStats(false);
    }
  };

  // Pod Message Storage handlers
  const handleGetStorageStats = async () => {
    try {
      setStorageStatsLoading(true);
      setStorageStats(null);
      const result = await mediacore.getMessageStorageStats();
      setStorageStats(result);
    } catch (err) {
      setStorageStats({ error: err.message });
      toast.error(`Failed to get storage stats: ${err.message}`);
    } finally {
      setStorageStatsLoading(false);
    }
  };

  const handleCleanupMessages = async () => {
    try {
      setCleanupLoading(true);
      const thirtyDaysAgo = Date.now() - (30 * 24 * 60 * 60 * 1000);
      const result = await mediacore.cleanupMessages(thirtyDaysAgo);
      toast.success(`Cleaned up ${result} old messages`);
      // Refresh stats after cleanup
      await handleGetStorageStats();
    } catch (err) {
      toast.error(`Failed to cleanup messages: ${err.message}`);
    } finally {
      setCleanupLoading(false);
    }
  };

  const handleRebuildSearchIndex = async () => {
    try {
      setRebuildIndexLoading(true);
      const result = await mediacore.rebuildSearchIndex();
      toast.success(result ? 'Search index rebuilt successfully' : 'Search index rebuild failed');
    } catch (err) {
      toast.error(`Failed to rebuild search index: ${err.message}`);
    } finally {
      setRebuildIndexLoading(false);
    }
  };

  const handleVacuumDatabase = async () => {
    try {
      setVacuumLoading(true);
      const result = await mediacore.vacuumDatabase();
      toast.success(result ? 'Database vacuum completed successfully' : 'Database vacuum failed');
    } catch (err) {
      toast.error(`Failed to vacuum database: ${err.message}`);
    } finally {
      setVacuumLoading(false);
    }
  };

  const handleSearchMessages = async () => {
    if (!searchQuery.trim()) return;

    try {
      setSearchLoading(true);
      setSearchResults(null);
      const result = await mediacore.searchMessages('all', searchQuery, null, 50); // Search all pods
      setSearchResults(result);
    } catch (err) {
      setSearchResults([]);
      toast.error(`Failed to search messages: ${err.message}`);
    } finally {
      setSearchLoading(false);
    }
  };

  // Pod Message Backfill handlers
  const handleGetBackfillStats = async () => {
    try {
      setBackfillStatsLoading(true);
      setBackfillStats(null);
      const result = await mediacore.getBackfillStats();
      setBackfillStats(result);
    } catch (err) {
      setBackfillStats({ error: err.message });
      toast.error(`Failed to get backfill stats: ${err.message}`);
    } finally {
      setBackfillStatsLoading(false);
    }
  };

  const handleSyncPodBackfill = async () => {
    if (!backfillPodId.trim()) {
      toast.error('Pod ID is required for backfill sync');
      return;
    }

    try {
      setSyncBackfillLoading(true);
      // Get current last seen timestamps
      const timestamps = await mediacore.getLastSeenTimestamps(backfillPodId);
      const result = await mediacore.syncPodBackfill(backfillPodId, timestamps);
      toast.success(`Backfill sync completed: ${result.totalMessagesReceived} messages received`);
      // Refresh stats
      await handleGetBackfillStats();
    } catch (err) {
      toast.error(`Failed to sync pod backfill: ${err.message}`);
    } finally {
      setSyncBackfillLoading(false);
    }
  };

  const handleGetLastSeenTimestamps = async () => {
    if (!backfillPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    try {
      const timestamps = await mediacore.getLastSeenTimestamps(backfillPodId);
      setLastSeenTimestamps(timestamps);
    } catch (err) {
      toast.error(`Failed to get last seen timestamps: ${err.message}`);
      setLastSeenTimestamps(null);
    }
  };

  // Pod Channel Management handlers
  const handleGetChannels = async () => {
    if (!channelPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    try {
      setChannelsLoading(true);
      const result = await mediacore.getChannels(channelPodId);
      setChannels(result);
    } catch (err) {
      toast.error(`Failed to get channels: ${err.message}`);
      setChannels([]);
    } finally {
      setChannelsLoading(false);
    }
  };

  const handleCreateChannel = async () => {
    if (!channelPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    if (!newChannelName.trim()) {
      toast.error('Channel name is required');
      return;
    }

    try {
      setCreateChannelLoading(true);
      const channel = {
        name: newChannelName,
        kind: newChannelKind,
      };
      await mediacore.createChannel(channelPodId, channel);
      toast.success(`Channel "${newChannelName}" created successfully`);
      setNewChannelName('');
      // Refresh channels list
      await handleGetChannels();
    } catch (err) {
      toast.error(`Failed to create channel: ${err.message}`);
    } finally {
      setCreateChannelLoading(false);
    }
  };

  const handleUpdateChannel = async (channelId) => {
    if (!editChannelName.trim()) {
      toast.error('Channel name is required');
      return;
    }

    try {
      setUpdateChannelLoading(true);
      const updatedChannel = {
        channelId: channelId,
        name: editChannelName,
        kind: editingChannel.kind,
      };
      await mediacore.updateChannel(channelPodId, channelId, updatedChannel);
      toast.success(`Channel updated successfully`);
      setEditingChannel(null);
      setEditChannelName('');
      // Refresh channels list
      await handleGetChannels();
    } catch (err) {
      toast.error(`Failed to update channel: ${err.message}`);
    } finally {
      setUpdateChannelLoading(false);
    }
  };

  const handleDeleteChannel = async (channelId, channelName) => {
    if (!confirm(`Are you sure you want to delete the channel "${channelName}"? This action cannot be undone.`)) {
      return;
    }

    try {
      setDeleteChannelLoading(true);
      await mediacore.deleteChannel(channelPodId, channelId);
      toast.success(`Channel "${channelName}" deleted successfully`);
      // Refresh channels list
      await handleGetChannels();
    } catch (err) {
      toast.error(`Failed to delete channel: ${err.message}`);
    } finally {
      setDeleteChannelLoading(false);
    }
  };

  const startEditingChannel = (channel) => {
    setEditingChannel(channel);
    setEditChannelName(channel.name);
  };

  const cancelEditingChannel = () => {
    setEditingChannel(null);
    setEditChannelName('');
  };

  // Pod Content Linking handlers
  const handleValidateContentId = async () => {
    if (!contentId.trim()) {
      toast.error('Content ID is required');
      return;
    }

    try {
      setContentValidationLoading(true);
      setContentValidation(null);
      setContentMetadata(null);
      const result = await mediacore.validateContentId(contentId.trim());
      setContentValidation(result);

      // If valid, automatically fetch metadata
      if (result.isValid) {
        await handleGetContentMetadata();
      }
    } catch (err) {
      setContentValidation({ isValid: false, error: err.message });
      toast.error(`Failed to validate content ID: ${err.message}`);
    } finally {
      setContentValidationLoading(false);
    }
  };

  const handleGetContentMetadata = async () => {
    if (!contentId.trim()) return;

    try {
      setContentMetadataLoading(true);
      const metadata = await mediacore.getContentMetadata(contentId.trim());
      setContentMetadata(metadata);

      // Auto-fill pod name if empty
      if (!newPodName.trim() && metadata) {
        setNewPodName(`${metadata.artist} - ${metadata.title}`);
      }
    } catch (err) {
      toast.error(`Failed to get content metadata: ${err.message}`);
      setContentMetadata(null);
    } finally {
      setContentMetadataLoading(false);
    }
  };

  const handleSearchContent = async () => {
    if (!contentSearchQuery.trim()) return;

    try {
      setContentSearchLoading(true);
      setContentSearchResults([]);
      const results = await mediacore.searchContent(contentSearchQuery.trim(), null, 10);
      setContentSearchResults(results);
    } catch (err) {
      toast.error(`Failed to search content: ${err.message}`);
      setContentSearchResults([]);
    } finally {
      setContentSearchLoading(false);
    }
  };

  const handleCreateContentLinkedPod = async () => {
    if (!contentId.trim()) {
      toast.error('Content ID is required');
      return;
    }

    if (!newPodName.trim()) {
      toast.error('Pod name is required');
      return;
    }

    if (!contentValidation?.isValid) {
      toast.error('Please validate the content ID first');
      return;
    }

    try {
      setCreatePodLoading(true);
      const podRequest = {
        podId: '', // Auto-generate
        name: newPodName.trim(),
        visibility: newPodVisibility,
        contentId: contentId.trim(),
        tags: [],
        channels: [
          {
            channelId: 'general',
            name: 'General',
            kind: 'General'
          }
        ],
        externalBindings: []
      };

      const createdPod = await mediacore.createContentLinkedPod(podRequest);
      toast.success(`Pod "${createdPod.name}" created successfully!`);

      // Reset form
      setContentId('');
      setContentValidation(null);
      setContentMetadata(null);
      setNewPodName('');
      setContentSearchQuery('');
      setContentSearchResults([]);

    } catch (err) {
      toast.error(`Failed to create pod: ${err.message}`);
    } finally {
      setCreatePodLoading(false);
    }
  };

  const selectContentFromSearch = (contentItem) => {
    setContentId(contentItem.contentId);
    setContentSearchQuery('');
    setContentSearchResults([]);
  };

  // Pod Opinion Management handlers
  const handlePublishOpinion = async () => {
    if (!opinionPodId.trim() || !opinionContentId.trim() || !opinionVariantHash.trim()) {
      toast.error('Pod ID, Content ID, and Variant Hash are required');
      return;
    }

    if (opinionScore < 0 || opinionScore > 10) {
      toast.error('Score must be between 0 and 10');
      return;
    }

    try {
      setPublishOpinionLoading(true);
      const opinion = {
        contentId: opinionContentId.trim(),
        variantHash: opinionVariantHash.trim(),
        score: opinionScore,
        note: opinionNote.trim(),
        senderPeerId: 'current-user', // TODO: Get from session
      };

      await mediacore.publishOpinion(opinionPodId.trim(), opinion);
      toast.success('Opinion published successfully');

      // Reset form
      setOpinionContentId('');
      setOpinionVariantHash('');
      setOpinionScore(5);
      setOpinionNote('');

      // Refresh opinions if we're viewing them
      if (opinionContentId) {
        await handleGetOpinions();
      }

    } catch (err) {
      toast.error(`Failed to publish opinion: ${err.message}`);
    } finally {
      setPublishOpinionLoading(false);
    }
  };

  const handleGetOpinions = async () => {
    if (!opinionPodId.trim() || !opinionContentId.trim()) {
      toast.error('Pod ID and Content ID are required');
      return;
    }

    try {
      setGetOpinionsLoading(true);
      const result = await mediacore.getContentOpinions(opinionPodId.trim(), opinionContentId.trim());
      setOpinions(result);
    } catch (err) {
      toast.error(`Failed to get opinions: ${err.message}`);
      setOpinions([]);
    } finally {
      setGetOpinionsLoading(false);
    }
  };

  const handleGetOpinionStatistics = async () => {
    if (!opinionPodId.trim() || !opinionContentId.trim()) {
      toast.error('Pod ID and Content ID are required');
      return;
    }

    try {
      setGetStatsLoading(true);
      const stats = await mediacore.getOpinionStatistics(opinionPodId.trim(), opinionContentId.trim());
      setOpinionStatistics(stats);
    } catch (err) {
      toast.error(`Failed to get opinion statistics: ${err.message}`);
      setOpinionStatistics(null);
    } finally {
      setGetStatsLoading(false);
    }
  };

  const handleRefreshOpinions = async () => {
    if (!opinionPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    try {
      setRefreshOpinionsLoading(true);
      const result = await mediacore.refreshPodOpinions(opinionPodId.trim());
      toast.success(`Refreshed ${result.opinionsRefreshed} opinions`);

      // Refresh current view
      if (opinionContentId) {
        await Promise.all([handleGetOpinions(), handleGetOpinionStatistics()]);
      }
    } catch (err) {
      toast.error(`Failed to refresh opinions: ${err.message}`);
    } finally {
      setRefreshOpinionsLoading(false);
    }
  };

  // Pod Opinion Aggregation handlers
  const handleGetAggregatedOpinions = async () => {
    if (!opinionPodId.trim() || !opinionContentId.trim()) {
      toast.error('Pod ID and Content ID are required');
      return;
    }

    try {
      setGetAggregatedLoading(true);
      const aggregated = await mediacore.getAggregatedOpinions(opinionPodId.trim(), opinionContentId.trim());
      setAggregatedOpinions(aggregated);
    } catch (err) {
      toast.error(`Failed to get aggregated opinions: ${err.message}`);
      setAggregatedOpinions(null);
    } finally {
      setGetAggregatedLoading(false);
    }
  };

  const handleGetMemberAffinities = async () => {
    if (!opinionPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    try {
      setGetAffinitiesLoading(true);
      const affinities = await mediacore.getMemberAffinities(opinionPodId.trim());
      setMemberAffinities(affinities);
    } catch (err) {
      toast.error(`Failed to get member affinities: ${err.message}`);
      setMemberAffinities({});
    } finally {
      setGetAffinitiesLoading(false);
    }
  };

  const handleGetConsensusRecommendations = async () => {
    if (!opinionPodId.trim() || !opinionContentId.trim()) {
      toast.error('Pod ID and Content ID are required');
      return;
    }

    try {
      setGetRecommendationsLoading(true);
      const recommendations = await mediacore.getConsensusRecommendations(opinionPodId.trim(), opinionContentId.trim());
      setConsensusRecommendations(recommendations);
    } catch (err) {
      toast.error(`Failed to get consensus recommendations: ${err.message}`);
      setConsensusRecommendations([]);
    } finally {
      setGetRecommendationsLoading(false);
    }
  };

  const handleUpdateMemberAffinities = async () => {
    if (!opinionPodId.trim()) {
      toast.error('Pod ID is required');
      return;
    }

    try {
      setUpdateAffinitiesLoading(true);
      const result = await mediacore.updateMemberAffinities(opinionPodId.trim());
      toast.success(`Updated affinities for ${result.membersUpdated} members`);

      // Refresh affinities display
      await handleGetMemberAffinities();
    } catch (err) {
      toast.error(`Failed to update member affinities: ${err.message}`);
    } finally {
      setUpdateAffinitiesLoading(false);
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
                    value={descriptorContentId}
                    onChange={(e) => setDescriptorContentId(e.target.value)}
                  />
                </Form.Field>
                <Button
                  primary
                  loading={registering}
                  disabled={!externalId.trim() || !descriptorContentId.trim() || registering}
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

              {descriptorVerificationResult && (
                <div style={{ marginTop: '1em' }}>
                  {descriptorVerificationResult.error ? (
                    <Message error>
                      <p>{descriptorVerificationResult.error}</p>
                    </Message>
                  ) : (
                    <Message success={descriptorVerificationResult.isValid} warning={!descriptorVerificationResult.isValid}>
                      <Message.Header>
                        Verification Result: {descriptorVerificationResult.isValid ? 'Valid' : 'Invalid'}
                      </Message.Header>
                      <p>
                        <strong>Signature Valid:</strong> {descriptorVerificationResult.signatureValid ? 'Yes' : 'No'}<br />
                        <strong>Freshness Valid:</strong> {descriptorVerificationResult.freshnessValid ? 'Yes' : 'No'}<br />
                        <strong>Age:</strong> {descriptorVerificationResult.age?.totalMinutes.toFixed(1)} minutes
                      </p>
                      {descriptorVerificationResult.warnings?.length > 0 && (
                        <div>
                          <strong>Warnings:</strong>
                          <List bulleted>
                            {descriptorVerificationResult.warnings.map((warning, index) => (
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

        {/* Pod Join/Leave */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="user plus" />
                Pod Join/Leave Operations
              </Card.Header>
              <Card.Description>
                Manage signed pod membership operations with cryptographic verification and role-based approvals
              </Card.Description>
            </Card.Content>

            {/* Join Request */}
            <Card.Content>
              <Header size="small">Request to Join Pod</Header>
              <Form>
                <Form.TextArea
                  label="Join Request JSON (signed by requester)"
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "requestedRole": "member", "publicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Please let me join!"}'
                  value={joinRequestData}
                  onChange={(e) => setJoinRequestData(e.target.value)}
                  rows={4}
                />
                <Button
                  primary
                  loading={requestingJoin}
                  disabled={requestingJoin || !joinRequestData.trim()}
                  onClick={handleRequestJoin}
                >
                  Submit Join Request
                </Button>
              </Form>

              {joinRequestResult && (
                <div style={{ marginTop: '1em' }}>
                  {joinRequestResult.error ? (
                    <Message error>
                      <p>Failed to submit join request: {joinRequestResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Join Request Submitted</Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {joinRequestResult.podId}<br />
                        <strong>Peer ID:</strong> {joinRequestResult.peerId}<br />
                        <strong>Status:</strong> {joinRequestResult.success ? 'Pending approval' : 'Failed'}
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Accept Join */}
                  <Header size="small">Accept Join Request</Header>
                  <Form>
                    <Form.TextArea
                      label="Acceptance JSON (signed by owner/mod)"
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "acceptedRole": "member", "acceptorPeerId": "bob", "acceptorPublicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Welcome!"}'
                      value={acceptanceData}
                      onChange={(e) => setAcceptanceData(e.target.value)}
                      rows={4}
                    />
                    <Button
                      positive
                      loading={acceptingJoin}
                      disabled={acceptingJoin || !acceptanceData.trim()}
                      onClick={handleAcceptJoin}
                    >
                      Accept Join
                    </Button>
                  </Form>

                  {acceptanceResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {acceptanceResult.error ? (
                        <Message error size="tiny">
                          <p>{acceptanceResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Join accepted successfully</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Leave Request */}
                  <Header size="small">Request to Leave Pod</Header>
                  <Form>
                    <Form.TextArea
                      label="Leave Request JSON (signed by member)"
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "publicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Goodbye!"}'
                      value={leaveRequestData}
                      onChange={(e) => setLeaveRequestData(e.target.value)}
                      rows={4}
                    />
                    <Button
                      loading={requestingLeave}
                      disabled={requestingLeave || !leaveRequestData.trim()}
                      onClick={handleRequestLeave}
                    >
                      Submit Leave Request
                    </Button>
                  </Form>

                  {leaveRequestResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {leaveRequestResult.error ? (
                        <Message error size="tiny">
                          <p>{leaveRequestResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Leave request submitted</p>
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
                  {/* Accept Leave */}
                  <Header size="small">Accept Leave Request (Owner/Mod Only)</Header>
                  <Form>
                    <Form.TextArea
                      label="Leave Acceptance JSON (signed by owner/mod)"
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "acceptorPeerId": "bob", "acceptorPublicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Farewell!"}'
                      value={acceptanceData}
                      onChange={(e) => setAcceptanceData(e.target.value)}
                      rows={4}
                    />
                    <Button
                      negative
                      loading={acceptingLeave}
                      disabled={acceptingLeave || !acceptanceData.trim()}
                      onClick={handleAcceptLeave}
                    >
                      Accept Leave
                    </Button>
                  </Form>

                  {leaveAcceptanceResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {leaveAcceptanceResult.error ? (
                        <Message error size="tiny">
                          <p>{leaveAcceptanceResult.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <p>Leave accepted successfully</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Pending Requests */}
                  <Header size="small">View Pending Requests</Header>
                  <Form>
                    <Form.Input
                      label="Pod ID"
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={pendingPodId}
                      onChange={(e) => setPendingPodId(e.target.value)}
                    />
                    <Button
                      loading={loadingPendingRequests}
                      disabled={loadingPendingRequests || !pendingPodId.trim()}
                      onClick={handleLoadPendingRequests}
                    >
                      Load Pending Requests
                    </Button>
                  </Form>

                  {pendingJoinRequests && !pendingJoinRequests.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <strong>Join Requests:</strong> {pendingJoinRequests.pendingJoinRequests?.length || 0}
                      </Message>
                    </div>
                  )}

                  {pendingLeaveRequests && !pendingLeaveRequests.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <strong>Leave Requests:</strong> {pendingLeaveRequests.pendingLeaveRequests?.length || 0}
                      </Message>
                    </div>
                  )}

                  {(pendingJoinRequests?.error || pendingLeaveRequests?.error) && (
                    <Message error size="tiny" style={{ marginTop: '0.5em' }}>
                      <p>Failed to load pending requests</p>
                    </Message>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Message Routing */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="send" />
                Pod Message Routing
              </Card.Header>
              <Card.Description>
                Decentralized message routing via overlay network with fanout and deduplication for reliable pod communication
              </Card.Description>
            </Card.Content>

            {/* Manual Message Routing */}
            <Card.Content>
              <Header size="small">Manual Message Routing</Header>
              <Form>
                <Form.TextArea
                  label="Pod Message JSON"
                  placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                  value={routeMessageData}
                  onChange={(e) => setRouteMessageData(e.target.value)}
                  rows={4}
                />
                <Button
                  primary
                  loading={routingMessage}
                  disabled={routingMessage || !routeMessageData.trim()}
                  onClick={handleRouteMessage}
                >
                  Route Message
                </Button>
              </Form>

              {routingResult && (
                <div style={{ marginTop: '1em' }}>
                  {routingResult.error ? (
                    <Message error>
                      <p>Failed to route message: {routingResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Message Routed Successfully</Message.Header>
                      <p>
                        <strong>Message ID:</strong> {routingResult.messageId}<br />
                        <strong>Pod ID:</strong> {routingResult.podId}<br />
                        <strong>Target Peers:</strong> {routingResult.targetPeerCount}<br />
                        <strong>Successfully Routed:</strong> {routingResult.successfullyRoutedCount}<br />
                        <strong>Failed:</strong> {routingResult.failedRoutingCount}<br />
                        <strong>Duration:</strong> {routingResult.routingDuration?.totalMilliseconds?.toFixed(0)}ms
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Route to Specific Peers */}
                  <Header size="small">Route to Specific Peers</Header>
                  <Form>
                    <Form.TextArea
                      label="Pod Message JSON"
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Direct message", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      value={routeToPeersMessage}
                      onChange={(e) => setRouteToPeersMessage(e.target.value)}
                      rows={3}
                    />
                    <Form.Input
                      label="Target Peer IDs (comma-separated)"
                      placeholder="bob,charlie,diana"
                      value={routeToPeersIds}
                      onChange={(e) => setRouteToPeersIds(e.target.value)}
                    />
                    <Button
                      fluid
                      loading={routingToPeers}
                      disabled={routingToPeers || !routeToPeersMessage.trim() || !routeToPeersIds.trim()}
                      onClick={handleRouteMessageToPeers}
                    >
                      Route to Peers
                    </Button>
                  </Form>

                  {routingToPeersResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {routingToPeersResult.error ? (
                        <Message error size="tiny">
                          <p>{routingToPeersResult.error}</p>
                        </Message>
                      ) : (
                        <Message info size="tiny">
                          <p>Routed to {routingToPeersResult.successfullyRoutedCount}/{routingToPeersResult.targetPeerCount} peers</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Message Deduplication */}
                  <Header size="small">Message Deduplication</Header>
                  <Form>
                    <Form.Group widths="equal">
                      <Form.Input
                        label="Message ID"
                        placeholder="msg123"
                        value={checkMessageId}
                        onChange={(e) => setCheckMessageId(e.target.value)}
                      />
                      <Form.Input
                        label="Pod ID"
                        placeholder="pod:artist:mb:daft-punk-hash"
                        value={checkPodId}
                        onChange={(e) => setCheckPodId(e.target.value)}
                      />
                    </Form.Group>
                    <Button.Group fluid>
                      <Button
                        loading={checkingMessageSeen}
                        disabled={checkingMessageSeen || !checkMessageId.trim() || !checkPodId.trim()}
                        onClick={handleCheckMessageSeen}
                      >
                        Check Seen
                      </Button>
                      <Button
                        color="blue"
                        onClick={handleRegisterMessageSeen}
                        disabled={!checkMessageId.trim() || !checkPodId.trim()}
                      >
                        Mark Seen
                      </Button>
                    </Button.Group>
                  </Form>

                  {messageSeenResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {messageSeenResult.error ? (
                        <Message error size="tiny">
                          <p>{messageSeenResult.error}</p>
                        </Message>
                      ) : (
                        <Message size="tiny">
                          <p>Message {messageSeenResult.isSeen ? 'has been' : 'has not been'} seen in pod {messageSeenResult.podId}</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>
              </Grid>
            </Card.Content>

            {/* Routing Statistics */}
            <Card.Content>
              <Button.Group fluid>
                <Button
                  primary
                  loading={loadingRoutingStats}
                  disabled={loadingRoutingStats}
                  onClick={handleLoadRoutingStats}
                >
                  Load Routing Stats
                </Button>
                <Button
                  color="red"
                  onClick={handleCleanupSeenMessages}
                >
                  Cleanup Seen Messages
                </Button>
              </Button.Group>

              {routingStats && !routingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Message Routing Statistics</Message.Header>
                    <p>
                      <strong>Total Messages Routed:</strong> {routingStats.totalMessagesRouted}<br />
                      <strong>Total Routing Attempts:</strong> {routingStats.totalRoutingAttempts}<br />
                      <strong>Successful Routes:</strong> {routingStats.successfulRoutingCount}<br />
                      <strong>Failed Routes:</strong> {routingStats.failedRoutingCount}<br />
                      <strong>Avg Routing Time:</strong> {routingStats.averageRoutingTimeMs.toFixed(2)}ms<br />
                      <strong>Deduplication Items:</strong> {routingStats.activeDeduplicationItems}<br />
                      <strong>Bloom Filter Fill:</strong> {(routingStats.bloomFilterFillRatio * 100).toFixed(1)}%<br />
                      <strong>Est. False Positive:</strong> {(routingStats.estimatedFalsePositiveRate * 100).toFixed(4)}%<br />
                      <strong>Last Operation:</strong> {routingStats.lastRoutingOperation ? new Date(routingStats.lastRoutingOperation).toLocaleString() : 'Never'}
                    </p>
                  </Message>

                  <Button
                    size="tiny"
                    color="blue"
                    onClick={() => handleRebuildSearchIndex()}
                    loading={rebuildIndexLoading}
                  >
                    Rebuild Search Index
                  </Button>
                  <Button
                    size="tiny"
                    color="orange"
                    onClick={() => handleVacuumDatabase()}
                    loading={vacuumLoading}
                  >
                    Vacuum Database
                  </Button>
                </div>
              )}

              {routingStats?.error && (
                <Message error style={{ marginTop: '1em' }}>
                  <p>Failed to load routing stats: {routingStats.error}</p>
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Message Storage */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="database" />
                Pod Message Storage
              </Card.Header>
              <Card.Description>
                SQLite-backed message storage with full-text search and retention policies
              </Card.Description>
            </Card.Content>

            {/* Message Storage */}
            <Card.Content>
              <Header size="small">Storage Management</Header>

              <div style={{ marginBottom: '1em' }}>
                <Button
                  size="small"
                  color="teal"
                  onClick={() => handleGetStorageStats()}
                  loading={storageStatsLoading}
                >
                  Get Storage Stats
                </Button>

                <Button
                  size="small"
                  color="purple"
                  onClick={() => handleCleanupMessages()}
                  loading={cleanupLoading}
                >
                  Cleanup Old Messages (30 days)
                </Button>

                <Button
                  size="small"
                  color="blue"
                  onClick={() => handleRebuildSearchIndex()}
                  loading={rebuildIndexLoading}
                >
                  Rebuild Search Index
                </Button>

                <Button
                  size="small"
                  color="orange"
                  onClick={() => handleVacuumDatabase()}
                  loading={vacuumLoading}
                >
                  Vacuum Database
                </Button>
              </div>

              {storageStats && (
                <Message size="small" style={{ marginBottom: '1em' }}>
                  <Message.Header>Message Storage Statistics</Message.Header>
                  <p>
                    <strong>Total Messages:</strong> {storageStats.totalMessages?.toLocaleString() || 0}<br />
                    <strong>Estimated Size:</strong> {(storageStats.totalSizeBytes / (1024 * 1024)).toFixed(2)} MB<br />
                    <strong>Oldest Message:</strong> {storageStats.oldestMessage ? new Date(storageStats.oldestMessage).toLocaleString() : 'None'}<br />
                    <strong>Newest Message:</strong> {storageStats.newestMessage ? new Date(storageStats.newestMessage).toLocaleString() : 'None'}<br />
                    <strong>Pods with Messages:</strong> {Object.keys(storageStats.messagesPerPod || {}).length}<br />
                    <strong>Active Channels:</strong> {Object.keys(storageStats.messagesPerChannel || {}).length}
                  </p>
                </Message>
              )}

              <Header size="small">Message Search</Header>
              <Input
                placeholder="Search messages..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                action={
                  <Button
                    color="green"
                    onClick={() => handleSearchMessages()}
                    loading={searchLoading}
                    disabled={!searchQuery.trim()}
                  >
                    Search
                  </Button>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {searchResults && searchResults.length > 0 && (
                <Message size="small">
                  <Message.Header>Search Results ({searchResults.length})</Message.Header>
                  <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                    {searchResults.map((msg, idx) => (
                      <div key={idx} style={{ marginBottom: '0.5em', padding: '0.5em', border: '1px solid #ddd', borderRadius: '4px' }}>
                        <small style={{ color: '#666' }}>
                          {new Date(msg.timestampUnixMs).toLocaleString()} • {msg.senderPeerId} • {msg.channelId}
                        </small>
                        <div style={{ marginTop: '0.25em' }}>{msg.body}</div>
                      </div>
                    ))}
                  </div>
                </Message>
              )}

              {searchResults && searchResults.length === 0 && searchQuery && (
                <Message size="small" warning>
                  No messages found matching "{searchQuery}"
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Message Backfill */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="sync" />
                Pod Message Backfill
              </Card.Header>
              <Card.Description>
                Synchronize missed messages when peers rejoin pods
              </Card.Description>
            </Card.Content>

            {/* Message Backfill */}
            <Card.Content>
              <Header size="small">Backfill Management</Header>

              <div style={{ marginBottom: '1em' }}>
                <Button
                  size="small"
                  color="purple"
                  onClick={() => handleGetBackfillStats()}
                  loading={backfillStatsLoading}
                >
                  Get Backfill Stats
                </Button>
              </div>

              {backfillStats && (
                <Message size="small" style={{ marginBottom: '1em' }}>
                  <Message.Header>Backfill Statistics</Message.Header>
                  <p>
                    <strong>Requests Sent:</strong> {backfillStats.totalBackfillRequestsSent?.toLocaleString() || 0}<br />
                    <strong>Requests Received:</strong> {backfillStats.totalBackfillRequestsReceived?.toLocaleString() || 0}<br />
                    <strong>Messages Backfilled:</strong> {backfillStats.totalMessagesBackfilled?.toLocaleString() || 0}<br />
                    <strong>Data Transferred:</strong> {(backfillStats.totalBackfillBytesTransferred / (1024 * 1024)).toFixed(2)} MB<br />
                    <strong>Avg Duration:</strong> {backfillStats.averageBackfillDurationMs?.toFixed(2) || 0}ms<br />
                    <strong>Last Operation:</strong> {backfillStats.lastBackfillOperation ? new Date(backfillStats.lastBackfillOperation).toLocaleString() : 'Never'}
                  </p>
                </Message>
              )}

              <Header size="small">Pod Backfill Sync</Header>
              <Input
                placeholder="Pod ID for backfill sync"
                value={backfillPodId}
                onChange={(e) => setBackfillPodId(e.target.value)}
                action={
                  <>
                    <Button
                      color="blue"
                      onClick={() => handleGetLastSeenTimestamps()}
                      disabled={!backfillPodId.trim()}
                    >
                      Get Timestamps
                    </Button>
                    <Button
                      color="green"
                      onClick={() => handleSyncPodBackfill()}
                      loading={syncBackfillLoading}
                      disabled={!backfillPodId.trim()}
                    >
                      Sync Backfill
                    </Button>
                  </>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {lastSeenTimestamps && Object.keys(lastSeenTimestamps).length > 0 && (
                <Message size="small">
                  <Message.Header>Last Seen Timestamps for Pod {backfillPodId}</Message.Header>
                  <div style={{ maxHeight: '150px', overflowY: 'auto' }}>
                    {Object.entries(lastSeenTimestamps).map(([channelId, timestamp]) => (
                      <div key={channelId} style={{ marginBottom: '0.25em' }}>
                        <strong>{channelId}:</strong> {new Date(timestamp).toLocaleString()}
                      </div>
                    ))}
                  </div>
                </Message>
              )}

              {lastSeenTimestamps && Object.keys(lastSeenTimestamps).length === 0 && (
                <Message size="small" info>
                  No last seen timestamps recorded for pod {backfillPodId}
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Channel Management */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="hashtag" />
                Pod Channel Management
              </Card.Header>
              <Card.Description>
                Create, update, and manage channels within pods for organized messaging
              </Card.Description>
            </Card.Content>

            {/* Channel Management */}
            <Card.Content>
              <Header size="small">Pod Channel Operations</Header>

              <Input
                placeholder="Pod ID for channel management"
                value={channelPodId}
                onChange={(e) => setChannelPodId(e.target.value)}
                action={
                  <Button
                    color="blue"
                    onClick={() => handleGetChannels()}
                    loading={channelsLoading}
                    disabled={!channelPodId.trim()}
                  >
                    Load Channels
                  </Button>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {/* Create New Channel */}
              <Header size="tiny">Create New Channel</Header>
              <Input
                placeholder="Channel name"
                value={newChannelName}
                onChange={(e) => setNewChannelName(e.target.value)}
                action={
                  <>
                    <select
                      value={newChannelKind}
                      onChange={(e) => setNewChannelKind(e.target.value)}
                      style={{ padding: '0.5em', border: '1px solid #ccc', borderRadius: '4px' }}
                    >
                      <option value="General">General</option>
                      <option value="Custom">Custom</option>
                      <option value="Bound">Bound</option>
                    </select>
                    <Button
                      color="green"
                      onClick={() => handleCreateChannel()}
                      loading={createChannelLoading}
                      disabled={!newChannelName.trim() || !channelPodId.trim()}
                    >
                      Create
                    </Button>
                  </>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {/* Channels List */}
              {channels.length > 0 && (
                <div>
                  <Header size="tiny">Existing Channels</Header>
                  <div style={{ maxHeight: '400px', overflowY: 'auto' }}>
                    {channels.map((channel) => (
                      <Card key={channel.channelId} style={{ marginBottom: '0.5em' }}>
                        <Card.Content style={{ padding: '0.5em' }}>
                          {editingChannel && editingChannel.channelId === channel.channelId ? (
                            <div>
                              <Input
                                placeholder="Channel name"
                                value={editChannelName}
                                onChange={(e) => setEditChannelName(e.target.value)}
                                action={
                                  <>
                                    <Button
                                      size="small"
                                      color="green"
                                      onClick={() => handleUpdateChannel(channel.channelId)}
                                      loading={updateChannelLoading}
                                      disabled={!editChannelName.trim()}
                                    >
                                      Save
                                    </Button>
                                    <Button
                                      size="small"
                                      onClick={() => cancelEditingChannel()}
                                    >
                                      Cancel
                                    </Button>
                                  </>
                                }
                                style={{ width: '100%' }}
                              />
                            </div>
                          ) : (
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                              <div>
                                <strong>{channel.name}</strong>
                                <div style={{ fontSize: '0.8em', color: '#666', marginTop: '0.25em' }}>
                                  ID: {channel.channelId} • Type: {channel.kind}
                                  {channel.bindingInfo && ` • Binding: ${channel.bindingInfo}`}
                                </div>
                              </div>
                              <div>
                                <Button
                                  size="tiny"
                                  onClick={() => startEditingChannel(channel)}
                                  disabled={channel.name.toLowerCase() === 'general' && channel.kind === 'General'}
                                >
                                  Edit
                                </Button>
                                <Button
                                  size="tiny"
                                  color="red"
                                  onClick={() => handleDeleteChannel(channel.channelId, channel.name)}
                                  loading={deleteChannelLoading}
                                  disabled={channel.name.toLowerCase() === 'general' && channel.kind === 'General'}
                                >
                                  Delete
                                </Button>
                              </div>
                            </div>
                          )}
                        </Card.Content>
                      </Card>
                    ))}
                  </div>
                </div>
              )}

              {channels.length === 0 && channelPodId && !channelsLoading && (
                <Message size="small" info>
                  No channels found in pod {channelPodId}. Create the first channel above.
                </Message>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Content Linking */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="linkify" />
                Pod Content Linking
              </Card.Header>
              <Card.Description>
                Create pods linked to specific content (music, videos, etc.) for focused discussions
              </Card.Description>
            </Card.Content>

            {/* Content Linking */}
            <Card.Content>
              <Header size="small">Content Search & Validation</Header>

              {/* Content Search */}
              <Input
                placeholder="Search for content (artist, album, movie, etc.)"
                value={contentSearchQuery}
                onChange={(e) => setContentSearchQuery(e.target.value)}
                action={
                  <Button
                    color="blue"
                    onClick={() => handleSearchContent()}
                    loading={contentSearchLoading}
                    disabled={!contentSearchQuery.trim()}
                  >
                    Search
                  </Button>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {/* Search Results */}
              {contentSearchResults.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Search Results</Header>
                  {contentSearchResults.map((item, idx) => (
                    <Card key={idx} style={{ marginBottom: '0.5em', cursor: 'pointer' }}
                          onClick={() => selectContentFromSearch(item)}>
                      <Card.Content style={{ padding: '0.5em' }}>
                        <strong>{item.title}</strong>
                        {item.subtitle && <div>{item.subtitle}</div>}
                        <small>{item.domain} • {item.type}</small>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}

              {/* Content Validation */}
              <Input
                placeholder="Content ID (e.g., content:audio:album:mb-release-id)"
                value={contentId}
                onChange={(e) => setContentId(e.target.value)}
                action={
                  <Button
                    color="green"
                    onClick={() => handleValidateContentId()}
                    loading={contentValidationLoading}
                    disabled={!contentId.trim()}
                  >
                    Validate
                  </Button>
                }
                style={{ width: '100%', marginBottom: '1em' }}
              />

              {/* Validation Result */}
              {contentValidation && (
                <Message size="small"
                         positive={contentValidation.isValid}
                         negative={!contentValidation.isValid}
                         style={{ marginBottom: '1em' }}>
                  <Message.Header>
                    {contentValidation.isValid ? '✓ Valid Content ID' : '✗ Invalid Content ID'}
                  </Message.Header>
                  {!contentValidation.isValid && contentValidation.errorMessage && (
                    <p>{contentValidation.errorMessage}</p>
                  )}
                </Message>
              )}

              {/* Content Metadata */}
              {contentMetadata && (
                <Message size="small" info style={{ marginBottom: '1em' }}>
                  <Message.Header>Content Metadata</Message.Header>
                  <p>
                    <strong>Title:</strong> {contentMetadata.title}<br />
                    <strong>Artist:</strong> {contentMetadata.artist}<br />
                    <strong>Type:</strong> {contentMetadata.type} ({contentMetadata.domain})
                  </p>
                </Message>
              )}

              {/* Pod Creation */}
              {contentValidation?.isValid && (
                <div>
                  <Header size="small">Create Content-Linked Pod</Header>

                  <Input
                    placeholder="Pod name (auto-filled from content)"
                    value={newPodName}
                    onChange={(e) => setNewPodName(e.target.value)}
                    style={{ width: '100%', marginBottom: '1em' }}
                  />

                  <div style={{ marginBottom: '1em' }}>
                    <label style={{ marginRight: '1em' }}>Visibility:</label>
                    <select
                      value={newPodVisibility}
                      onChange={(e) => setNewPodVisibility(e.target.value)}
                    >
                      <option value="Unlisted">Unlisted</option>
                      <option value="Listed">Listed</option>
                      <option value="Private">Private</option>
                    </select>
                  </div>

                  <Button
                    color="teal"
                    onClick={() => handleCreateContentLinkedPod()}
                    loading={createPodLoading}
                    disabled={!newPodName.trim()}
                  >
                    Create Content-Linked Pod
                  </Button>
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Opinion Management */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="star" />
                Pod Opinion Management
              </Card.Header>
              <Card.Description>
                Publish and view opinions on content variants within pods for quality assessment and community feedback
              </Card.Description>
            </Card.Content>

            {/* Opinion Management */}
            <Card.Content>
              <Header size="small">Opinion Management</Header>

              {/* Pod Selection */}
              <Input
                placeholder="Pod ID"
                value={opinionPodId}
                onChange={(e) => setOpinionPodId(e.target.value)}
                style={{ width: '100%', marginBottom: '1em' }}
              />

              <Button
                color="blue"
                onClick={() => handleRefreshOpinions()}
                loading={refreshOpinionsLoading}
                disabled={!opinionPodId.trim()}
                style={{ marginBottom: '1em' }}
              >
                Refresh Pod Opinions
              </Button>

              {/* Content Opinions */}
              <Header size="tiny">Content Opinions</Header>
              <Input
                placeholder="Content ID (e.g., content:audio:album:mb-id)"
                value={opinionContentId}
                onChange={(e) => setOpinionContentId(e.target.value)}
                style={{ width: '100%', marginBottom: '1em' }}
              />

              <div style={{ marginBottom: '1em' }}>
                <Button
                  color="teal"
                  onClick={() => handleGetOpinions()}
                  loading={getOpinionsLoading}
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Opinions
                </Button>

                <Button
                  color="purple"
                  onClick={() => handleGetOpinionStatistics()}
                  loading={getStatsLoading}
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                >
                  Get Statistics
                </Button>
              </div>

              {/* Opinion Statistics */}
              {opinionStatistics && (
                <Message info style={{ marginBottom: '1em' }}>
                  <Message.Header>Opinion Statistics</Message.Header>
                  <p>
                    <strong>Total Opinions:</strong> {opinionStatistics.totalOpinions}<br />
                    <strong>Unique Variants:</strong> {opinionStatistics.uniqueVariants}<br />
                    <strong>Average Score:</strong> {opinionStatistics.averageScore.toFixed(1)}<br />
                    <strong>Score Range:</strong> {opinionStatistics.minScore} - {opinionStatistics.maxScore}<br />
                    <strong>Last Updated:</strong> {new Date(opinionStatistics.lastUpdated).toLocaleString()}
                  </p>
                </Message>
              )}

              {/* Opinions List */}
              {opinions.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Opinions ({opinions.length})</Header>
                  {opinions.map((opinion, idx) => (
                    <Card key={idx} style={{ marginBottom: '0.5em' }}>
                      <Card.Content style={{ padding: '0.5em' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                          <div>
                            <strong>Variant:</strong> {opinion.variantHash.substring(0, 8)}...<br />
                            <strong>Score:</strong> {opinion.score}/10
                            {opinion.note && (
                              <>
                                <br /><strong>Note:</strong> {opinion.note}
                              </>
                            )}
                          </div>
                          <small>{opinion.senderPeerId}</small>
                        </div>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}

              {/* Publish Opinion */}
              <Header size="small">Publish New Opinion</Header>

              <Input
                placeholder="Variant Hash"
                value={opinionVariantHash}
                onChange={(e) => setOpinionVariantHash(e.target.value)}
                style={{ width: '100%', marginBottom: '1em' }}
              />

              <div style={{ marginBottom: '1em' }}>
                <label style={{ marginRight: '1em' }}>Score (0-10):</label>
                <input
                  type="range"
                  min="0"
                  max="10"
                  step="0.5"
                  value={opinionScore}
                  onChange={(e) => setOpinionScore(parseFloat(e.target.value))}
                  style={{ width: '200px' }}
                />
                <span style={{ marginLeft: '1em' }}>{opinionScore}/10</span>
              </div>

              <Input
                placeholder="Optional note about this variant"
                value={opinionNote}
                onChange={(e) => setOpinionNote(e.target.value)}
                style={{ width: '100%', marginBottom: '1em' }}
              />

              <Button
                color="green"
                onClick={() => handlePublishOpinion()}
                loading={publishOpinionLoading}
                disabled={!opinionPodId.trim() || !opinionContentId.trim() || !opinionVariantHash.trim()}
              >
                Publish Opinion
              </Button>
            </Card.Content>

            {/* Opinion Aggregation */}
            <Card.Content>
              <Header size="small">Opinion Aggregation & Consensus</Header>

              <div style={{ marginBottom: '1em' }}>
                <Button
                  color="purple"
                  onClick={() => handleGetAggregatedOpinions()}
                  loading={getAggregatedLoading}
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Aggregated Opinions
                </Button>

                <Button
                  color="blue"
                  onClick={() => handleGetMemberAffinities()}
                  loading={getAffinitiesLoading}
                  disabled={!opinionPodId.trim()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Member Affinities
                </Button>

                <Button
                  color="teal"
                  onClick={() => handleGetConsensusRecommendations()}
                  loading={getRecommendationsLoading}
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Recommendations
                </Button>

                <Button
                  color="orange"
                  onClick={() => handleUpdateMemberAffinities()}
                  loading={updateAffinitiesLoading}
                  disabled={!opinionPodId.trim()}
                >
                  Update Affinities
                </Button>
              </div>

              {/* Aggregated Opinions */}
              {aggregatedOpinions && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Aggregated Opinion Results</Header>
                  <Message info>
                    <strong>Weighted Average:</strong> {aggregatedOpinions.weightedAverageScore.toFixed(2)}/10<br />
                    <strong>Unweighted Average:</strong> {aggregatedOpinions.unweightedAverageScore.toFixed(2)}/10<br />
                    <strong>Consensus Strength:</strong> {(aggregatedOpinions.consensusStrength * 100).toFixed(1)}%<br />
                    <strong>Total Opinions:</strong> {aggregatedOpinions.totalOpinions}<br />
                    <strong>Unique Variants:</strong> {aggregatedOpinions.uniqueVariants}<br />
                    <strong>Contributing Members:</strong> {aggregatedOpinions.contributingMembers}
                  </Message>

                  {/* Variant Breakdown */}
                  {aggregatedOpinions.variantAggregates.length > 0 && (
                    <div style={{ marginTop: '1em' }}>
                      <Header size="tiny">Variant Analysis</Header>
                      {aggregatedOpinions.variantAggregates.map((variant, idx) => (
                        <Card key={idx} style={{ marginBottom: '0.5em' }}>
                          <Card.Content style={{ padding: '0.5em' }}>
                            <div>
                              <strong>Variant:</strong> {variant.variantHash.substring(0, 8)}...<br />
                              <strong>Weighted Score:</strong> {variant.weightedAverageScore.toFixed(2)}/10<br />
                              <strong>Unweighted Score:</strong> {variant.unweightedAverageScore.toFixed(2)}/10<br />
                              <strong>Opinions:</strong> {variant.opinionCount}<br />
                              <strong>Agreement:</strong> {(1 - variant.scoreStandardDeviation / 5).toFixed(2)} (lower std dev = higher agreement)
                            </div>
                          </Card.Content>
                        </Card>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* Consensus Recommendations */}
              {consensusRecommendations.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Consensus Recommendations</Header>
                  {consensusRecommendations.map((rec, idx) => (
                    <Card key={idx} style={{
                      marginBottom: '0.5em',
                      borderLeft: rec.recommendation === 'StronglyRecommended' ? '5px solid #21ba45' :
                                 rec.recommendation === 'Recommended' ? '5px solid #2185d0' :
                                 rec.recommendation === 'Neutral' ? '5px solid #fbbd08' :
                                 rec.recommendation === 'NotRecommended' ? '5px solid #f2711c' :
                                 '5px solid #db2828'
                    }}>
                      <Card.Content style={{ padding: '0.5em' }}>
                        <div>
                          <strong>Variant:</strong> {rec.variantHash.substring(0, 8)}...<br />
                          <strong>Recommendation:</strong> {rec.recommendation.replace(/([A-Z])/g, ' $1').trim()}<br />
                          <strong>Consensus Score:</strong> {(rec.consensusScore * 100).toFixed(1)}%<br />
                          <strong>Reasoning:</strong> {rec.reasoning}<br />
                          <small><strong>Factors:</strong> {rec.supportingFactors.join(', ')}</small>
                        </div>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}

              {/* Member Affinities */}
              {Object.keys(memberAffinities).length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Member Affinities ({Object.keys(memberAffinities).length})</Header>
                  {Object.entries(memberAffinities).map(([peerId, affinity], idx) => (
                    <Card key={idx} style={{ marginBottom: '0.5em' }}>
                      <Card.Content style={{ padding: '0.5em' }}>
                        <div>
                          <strong>Peer:</strong> {peerId.substring(0, 8)}...<br />
                          <strong>Affinity Score:</strong> {(affinity.affinityScore * 100).toFixed(1)}%<br />
                          <strong>Trust Score:</strong> {(affinity.trustScore * 100).toFixed(1)}%<br />
                          <strong>Messages:</strong> {affinity.messageCount}<br />
                          <strong>Opinions:</strong> {affinity.opinionCount}<br />
                          <small>Last Activity: {new Date(affinity.lastActivity).toLocaleDateString()}</small>
                        </div>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Pod Message Signing */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="key" />
                Pod Message Signing
              </Card.Header>
              <Card.Description>
                Cryptographic signing and verification of pod messages for authenticity and integrity
              </Card.Description>
            </Card.Content>

            {/* Message Signing */}
            <Card.Content>
              <Header size="small">Sign Pod Message</Header>
              <Form>
                <Form.TextArea
                  label="Pod Message JSON"
                  placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789}'
                  value={messageToSign}
                  onChange={(e) => setMessageToSign(e.target.value)}
                  rows={3}
                />
                <Form.Input
                  label="Private Key"
                  type="password"
                  placeholder="base64-encoded private key"
                  value={privateKeyForSigning}
                  onChange={(e) => setPrivateKeyForSigning(e.target.value)}
                />
                <Button
                  primary
                  loading={signingMessage}
                  disabled={signingMessage || !messageToSign.trim() || !privateKeyForSigning.trim()}
                  onClick={handleSignMessage}
                >
                  Sign Message
                </Button>
              </Form>

              {signedMessageResult && (
                <div style={{ marginTop: '1em' }}>
                  {signedMessageResult.error ? (
                    <Message error>
                      <p>Failed to sign message: {signedMessageResult.error}</p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Message Signed Successfully</Message.Header>
                      <p>
                        <strong>Message ID:</strong> {signedMessageResult.messageId}<br />
                        <strong>Channel:</strong> {signedMessageResult.channelId}<br />
                        <strong>Signature:</strong> {signedMessageResult.signature?.substring(0, 50)}...
                      </p>
                    </Message>
                  )}
                </div>
              )}
            </Card.Content>

            <Card.Content>
              <Grid>
                <Grid.Column width={8}>
                  {/* Signature Verification */}
                  <Header size="small">Verify Message Signature</Header>
                  <Form>
                    <Form.TextArea
                      label="Pod Message JSON (with signature)"
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      value={messageToVerify}
                      onChange={(e) => setMessageToVerify(e.target.value)}
                      rows={4}
                    />
                    <Button
                      fluid
                      loading={verifyingSignature}
                      disabled={verifyingSignature || !messageToVerify.trim()}
                      onClick={handleVerifySignature}
                    >
                      Verify Signature
                    </Button>
                  </Form>

                  {verificationResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {verificationResult.error ? (
                        <Message error size="tiny">
                          <p>{verificationResult.error}</p>
                        </Message>
                      ) : (
                        <Message size="tiny">
                          <p>Message {verificationResult.messageId}: Signature is {verificationResult.isValid ? 'VALID' : 'INVALID'}</p>
                        </Message>
                      )}
                    </div>
                  )}
                </Grid.Column>

                <Grid.Column width={8}>
                  {/* Key Pair Generation */}
                  <Header size="small">Generate Key Pair</Header>
                  <Form>
                    <Button
                      fluid
                      loading={generatingKeyPair}
                      disabled={generatingKeyPair}
                      onClick={handleGenerateKeyPair}
                    >
                      Generate New Key Pair
                    </Button>
                  </Form>

                  {generatedKeyPair && (
                    <div style={{ marginTop: '0.5em' }}>
                      {generatedKeyPair.error ? (
                        <Message error size="tiny">
                          <p>{generatedKeyPair.error}</p>
                        </Message>
                      ) : (
                        <Message success size="tiny">
                          <Message.Header>Key Pair Generated</Message.Header>
                          <p>
                            <strong>Public Key:</strong> {generatedKeyPair.publicKey?.substring(0, 30)}...<br />
                            <strong>Private Key:</strong> {generatedKeyPair.privateKey?.substring(0, 30)}...<br />
                            <em>⚠️ Keep private key secure!</em>
                          </p>
                        </Message>
                      )}
                    </div>
                  )}

                  {/* Signing Statistics */}
                  <Header size="small" style={{ marginTop: '1em' }}>Signing Statistics</Header>
                  <Button.Group fluid>
                    <Button
                      loading={loadingSigningStats}
                      disabled={loadingSigningStats}
                      onClick={handleLoadSigningStats}
                    >
                      Load Stats
                    </Button>
                  </Button.Group>

                  {signingStats && !signingStats.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <p>
                          <strong>Signatures Created:</strong> {signingStats.totalSignaturesCreated}<br />
                          <strong>Signatures Verified:</strong> {signingStats.totalSignaturesVerified}<br />
                          <strong>Successful:</strong> {signingStats.successfulVerifications}<br />
                          <strong>Failed:</strong> {signingStats.failedVerifications}<br />
                          <strong>Avg Sign Time:</strong> {signingStats.averageSigningTimeMs.toFixed(2)}ms<br />
                          <strong>Avg Verify Time:</strong> {signingStats.averageVerificationTimeMs.toFixed(2)}ms
                        </p>
                      </Message>
                    </div>
                  )}

                  {signingStats?.error && (
                    <Message error size="tiny" style={{ marginTop: '0.5em' }}>
                      <p>{signingStats.error}</p>
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
