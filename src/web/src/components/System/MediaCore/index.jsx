import * as mediacore from '../../../lib/mediacore';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Card,
  Dropdown,
  Form,
  Grid,
  Header,
  Icon,
  Input,
  Label,
  List,
  Loader,
  Message,
  Segment,
  Statistic,
  TextArea,
} from 'semantic-ui-react';

// Predefined examples for different domains
const contentExamples = {
  audio: {
    album: {
      content: 'content:audio:album:mb-67890',
      external: 'mb:release:67890',
    },
    artist: {
      content: 'content:audio:artist:mb-abc123',
      external: 'mb:artist:abc123',
    },
    track: {
      content: 'content:audio:track:mb-12345',
      external: 'mb:recording:12345',
    },
  },
  image: {
    artwork: {
      content: 'content:image:artwork:discogs-11111',
      external: 'discogs:release:11111',
    },
    photo: {
      content: 'content:image:photo:flickr-67890',
      external: 'flickr:photo:67890',
    },
  },
  video: {
    movie: {
      content: 'content:video:movie:imdb-tt0111161',
      external: 'imdb:tt0111161',
    },
    series: {
      content: 'content:video:series:tvdb-12345',
      external: 'tvdb:series:12345',
    },
  },
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
  const [sampleRate, setSampleRate] = useState(44_100);
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
  const [perceptualSimilarityResult, setPerceptualSimilarityResult] =
    useState(null);
  const [findSimilarResult, setFindSimilarResult] = useState(null);
  const [textSimilarityResult, setTextSimilarityResult] = useState(null);
  const [computingPerceptualSimilarity, setComputingPerceptualSimilarity] =
    useState(false);
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
  const [descriptorVerificationResult, setDescriptorVerificationResult] =
    useState(null);
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
  const [metadataPortabilityStats, setMetadataPortabilityStats] =
    useState(null);
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
  const [verifyingMembershipStatus, setVerifyingMembershipStatus] =
    useState(false);
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
  const [membershipVerificationResult, setMembershipVerificationResult] =
    useState(null);
  const [membershipMessageToVerify, setMembershipMessageToVerify] =
    useState('');
  const [verifyingMessage, setVerifyingMessage] = useState(false);
  const [messageVerificationResult, setMessageVerificationResult] =
    useState(null);
  const [roleCheckPodId, setRoleCheckPodId] = useState('');
  const [roleCheckPeerId, setRoleCheckPeerId] = useState('');
  const [requiredRole, setRequiredRole] = useState('member');
  const [checkingRole, setCheckingRole] = useState(false);
  const [roleCheckResult, setRoleCheckResult] = useState(null);
  const [verificationStats, setVerificationStats] = useState(null);
  const [loadingVerificationStats, setLoadingVerificationStats] =
    useState(false);

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
  const [contentValidationLoading, setContentValidationLoading] =
    useState(false);
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
  const [getRecommendationsLoading, setGetRecommendationsLoading] =
    useState(false);
  const [updateAffinitiesLoading, setUpdateAffinitiesLoading] = useState(false);
  const [publishContentId, setPublishContentId] = useState('');
  const [publishCodec, setPublishCodec] = useState('mp3');
  const [publishSize, setPublishSize] = useState(1_024);
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
      } catch (error_) {
        setError(error_.message);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();

    // Refresh stats every 60 seconds
    const interval = setInterval(fetchStats, 60_000);
    return () => clearInterval(interval);
  }, []);

  const handleRegister = async () => {
    if (!externalId.trim() || !descriptorContentId.trim()) return;

    try {
      setRegistering(true);
      await mediacore.registerContentId(
        externalId.trim(),
        descriptorContentId.trim(),
      );
      setExternalId('');
      setDescriptorContentId('');
      setContentId('');

      // Refresh stats
      const data = await mediacore.getContentIdStats();
      setStats(data);
    } catch (error_) {
      setError(`Failed to register: ${error_.message}`);
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
    } catch (error_) {
      setResolvedContent({ error: error_.message });
    } finally {
      setResolving(false);
    }
  };

  const handleValidate = async () => {
    if (!validateContentIdInput.trim()) return;

    try {
      setValidating(true);
      setValidatedContent(null);
      const result = await mediacore.validateContentId(
        validateContentIdInput.trim(),
      );
      setValidatedContent(result);
    } catch (error_) {
      setValidatedContent({ error: error_.message });
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
        ? await mediacore.findContentIdsByDomainAndType(
            domain.trim(),
            type.trim(),
          )
        : await mediacore.findContentIdsByDomain(domain.trim());
      setDomainResults(result);
    } catch (error_) {
      setDomainResults({ error: error_.message });
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
      const result = await mediacore.traverseContentGraph(
        traverseContentId.trim(),
        traverseLinkName.trim(),
      );
      setTraversalResults(result);
    } catch (error_) {
      setTraversalResults({ error: error_.message });
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
    } catch (error_) {
      setGraphResults({ error: error_.message });
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
    } catch (error_) {
      setInboundResults({ error: error_.message });
    } finally {
      setFindingInbound(false);
    }
  };

  const loadSupportedAlgorithms = async () => {
    try {
      const result = await mediacore.getSupportedHashAlgorithms();
      setSupportedAlgorithms(result);
    } catch (error_) {
      console.error('Failed to load hash algorithms:', error_);
    }
  };

  const handleComputeAudioHash = async () => {
    if (!audioSamples.trim()) return;

    try {
      setComputingAudioHash(true);
      setAudioHashResult(null);

      // Parse comma-separated float values
      const samples = audioSamples
        .split(',')
        .map((s) => Number.parseFloat(s.trim()))
        .filter((n) => !isNaN(n));

      if (samples.length === 0) {
        throw new Error('No valid audio samples provided');
      }

      const result = await mediacore.computeAudioHash(
        samples,
        Number.parseInt(sampleRate),
        audioAlgorithm,
      );
      setAudioHashResult(result);
    } catch (error_) {
      setAudioHashResult({ error: error_.message });
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
      const pixels = imagePixels
        .split(',')
        .map((s) => Number.parseInt(s.trim()))
        .filter((n) => !isNaN(n) && n >= 0 && n <= 255);

      if (pixels.length === 0) {
        throw new Error('No valid pixel data provided');
      }

      const result = await mediacore.computeImageHash(
        pixels,
        Number.parseInt(imageWidth),
        Number.parseInt(imageHeight),
        imageAlgorithm,
      );
      setImageHashResult(result);
    } catch (error_) {
      setImageHashResult({ error: error_.message });
    } finally {
      setComputingImageHash(false);
    }
  };

  const handleComputeSimilarity = async () => {
    if (!hashA.trim() || !hashB.trim()) return;

    try {
      setComputingSimilarity(true);
      setSimilarityResult(null);
      const result = await mediacore.computeHashSimilarity(
        hashA.trim(),
        hashB.trim(),
        Number.parseFloat(similarityThreshold),
      );
      setSimilarityResult(result);
    } catch (error_) {
      setSimilarityResult({ error: error_.message });
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
        Number.parseFloat(perceptualThreshold),
      );
      setPerceptualSimilarityResult(result);
    } catch (error_) {
      setPerceptualSimilarityResult({ error: error_.message });
    } finally {
      setComputingPerceptualSimilarity(false);
    }
  };

  const handleFindSimilarContent = async () => {
    if (!findSimilarContentId.trim()) return;

    try {
      setFindingSimilarContent(true);
      setFindSimilarResult(null);
      const result = await mediacore.findSimilarContent(
        findSimilarContentId.trim(),
        {
          maxResults: Number.parseInt(findSimilarMaxResults),
          minConfidence: Number.parseFloat(findSimilarMinConfidence),
        },
      );
      setFindSimilarResult(result);
    } catch (error_) {
      setFindSimilarResult({ error: error_.message });
    } finally {
      setFindingSimilarContent(false);
    }
  };

  const handleComputeTextSimilarity = async () => {
    if (!textSimilarityA.trim() || !textSimilarityB.trim()) return;

    try {
      setComputingTextSimilarity(true);
      setTextSimilarityResult(null);
      const result = await mediacore.computeTextSimilarity(
        textSimilarityA.trim(),
        textSimilarityB.trim(),
      );
      setTextSimilarityResult(result);
    } catch (error_) {
      setTextSimilarityResult({ error: error_.message });
    } finally {
      setComputingTextSimilarity(false);
    }
  };

  const handleExportMetadata = async () => {
    const contentIds = exportContentIds
      .split('\n')
      .map((id) => id.trim())
      .filter(Boolean);
    if (!contentIds.length) return;

    try {
      setExportingMetadata(true);
      setExportResult(null);
      const result = await mediacore.exportMetadata(contentIds, includeLinks);
      setExportResult(result);
    } catch (error_) {
      setExportResult({ error: error_.message });
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
      } catch {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.importMetadata(
        packageData,
        conflictStrategy,
        dryRun,
      );
      setImportResult(result);
    } catch (error_) {
      setImportResult({ error: error_.message });
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
      } catch {
        throw new Error('Invalid JSON format for metadata package');
      }

      const result = await mediacore.analyzeMetadataConflicts(packageData);
      setConflictAnalysis(result);
    } catch (error_) {
      setConflictAnalysis({ error: error_.message });
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
        codec: publishCodec.trim(),
        confidence: 0.8,
        contentId: publishContentId.trim(),
        sizeBytes: Number.parseInt(publishSize),
      };

      const result = await mediacore.publishContentDescriptor(descriptor);
      setPublishResult(result);
    } catch (error_) {
      setPublishResult({ error: error_.message });
    } finally {
      setPublishingDescriptor(false);
    }
  };

  const handlePublishBatch = async () => {
    const contentIds = batchContentIds
      .split('\n')
      .map((id) => id.trim())
      .filter(Boolean);
    if (!contentIds.length) return;

    try {
      setPublishingBatch(true);
      setBatchPublishResult(null);

      // Create mock descriptors for each ContentID
      const descriptors = contentIds.map((contentId) => ({
        // 1MB mock
        codec: 'mock',

        confidence: 0.8,
        contentId,
        sizeBytes: 1_024 * 1_024,
      }));

      const result =
        await mediacore.publishContentDescriptorsBatch(descriptors);
      setBatchPublishResult(result);
    } catch (error_) {
      setBatchPublishResult({ error: error_.message });
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
      if (updateSize.trim()) updates.newSizeBytes = Number.parseInt(updateSize);
      if (updateConfidence.trim())
        updates.newConfidence = Number.parseFloat(updateConfidence);

      if (Object.keys(updates).length === 0) {
        throw new Error('At least one update field is required');
      }

      const result = await mediacore.updateContentDescriptor(
        updateTargetId.trim(),
        updates,
      );
      setUpdateResult(result);
    } catch (error_) {
      setUpdateResult({ error: error_.message });
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
    } catch (error_) {
      setRepublishResult({ error: error_.message });
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
    } catch (error_) {
      setPublishingStats({ error: error_.message });
    } finally {
      setLoadingStats(false);
    }
  };

  const handleRetrieveDescriptor = async () => {
    if (!retrieveContentId.trim()) return;

    try {
      setRetrievingDescriptor(true);
      setRetrievalResult(null);
      const result = await mediacore.retrieveContentDescriptor(
        retrieveContentId.trim(),
        bypassCache,
      );
      setRetrievalResult(result);
    } catch (error_) {
      setRetrievalResult({ error: error_.message });
    } finally {
      setRetrievingDescriptor(false);
    }
  };

  const handleRetrieveBatch = async () => {
    const contentIds = batchRetrieveContentIds
      .split('\n')
      .map((id) => id.trim())
      .filter(Boolean);
    if (!contentIds.length) return;

    try {
      setRetrievingBatch(true);
      setBatchRetrievalResult(null);
      const result =
        await mediacore.retrieveContentDescriptorsBatch(contentIds);
      setBatchRetrievalResult(result);
    } catch (error_) {
      setBatchRetrievalResult({ error: error_.message });
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
        Number.parseInt(queryMaxResults),
      );
      setQueryResult(result);
    } catch (error_) {
      setQueryResult({ error: error_.message });
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
      } catch {
        throw new Error('Invalid JSON format for descriptor');
      }

      const result = await mediacore.verifyContentDescriptor(descriptor);
      setVerificationResult(result);
    } catch (error_) {
      setVerificationResult({ error: error_.message });
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
    } catch (error_) {
      setRetrievalStats({ error: error_.message });
    } finally {
      setLoadingRetrievalStats(false);
    }
  };

  const handleClearRetrievalCache = async () => {
    try {
      const result = await mediacore.clearRetrievalCache();
      // Reload stats to reflect changes
      await handleLoadRetrievalStats();
      toast.success(
        `Cache cleared: ${result.entriesCleared} entries, ${result.bytesFreed} bytes freed`,
      );
    } catch (error_) {
      toast.error(`Failed to clear cache: ${error_.message}`);
    }
  };

  const handleLoadMediaCoreDashboard = async () => {
    try {
      setLoadingDashboard(true);
      setMediaCoreDashboard(null);
      const result = await mediacore.getMediaCoreDashboard();
      setMediaCoreDashboard(result);
    } catch (error_) {
      setMediaCoreDashboard({ error: error_.message });
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
    } catch (error_) {
      setContentRegistryStats({ error: error_.message });
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
    } catch (error_) {
      setDescriptorStats({ error: error_.message });
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
    } catch (error_) {
      setFuzzyMatchingStats({ error: error_.message });
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
    } catch (error_) {
      setIpldMappingStats({ error: error_.message });
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
    } catch (error_) {
      setPerceptualHashingStats({ error: error_.message });
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
    } catch (error_) {
      setMetadataPortabilityStats({ error: error_.message });
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
    } catch (error_) {
      setContentPublishingStats({ error: error_.message });
    } finally {
      setLoadingPublishingStats(false);
    }
  };

  const handleResetMediaCoreStats = async () => {
    if (
      !confirm(
        'Are you sure you want to reset all MediaCore statistics? This cannot be undone.',
      )
    ) {
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
      toast.success('MediaCore statistics have been reset');
    } catch (error_) {
      toast.error(`Failed to reset stats: ${error_.message}`);
    }
  };

  // PodCore handlers
  const handlePublishPod = async () => {
    if (!podToPublish.trim()) {
      toast.warning('Please enter pod JSON data');
      return;
    }

    try {
      setPublishingPod(true);
      setPodPublishingResult(null);
      const pod = JSON.parse(podToPublish);
      const result = await mediacore.publishPod(pod);
      setPodPublishingResult(result);
      setPodToPublish('');
    } catch (error_) {
      setPodPublishingResult({ error: error_.message });
    } finally {
      setPublishingPod(false);
    }
  };

  const handleRetrievePodMetadata = async () => {
    if (!podMetadataToRetrieve.trim()) {
      toast.warning('Please enter a pod ID');
      return;
    }

    try {
      setRetrievingPodMetadata(true);
      setPodMetadataResult(null);
      const result = await mediacore.getPublishedPodMetadata(
        podMetadataToRetrieve,
      );
      setPodMetadataResult(result);
    } catch (error_) {
      setPodMetadataResult({ error: error_.message });
    } finally {
      setRetrievingPodMetadata(false);
    }
  };

  const handleUnpublishPod = async () => {
    if (!podToUnpublish.trim()) {
      toast.warning('Please enter a pod ID');
      return;
    }

    if (
      !confirm(`Are you sure you want to unpublish pod "${podToUnpublish}"?`)
    ) {
      return;
    }

    try {
      setUnpublishingPod(true);
      setPodUnpublishResult(null);
      const result = await mediacore.unpublishPod(podToUnpublish);
      setPodUnpublishResult(result);
      setPodToUnpublish('');
    } catch (error_) {
      setPodUnpublishResult({ error: error_.message });
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
    } catch (error_) {
      setPodPublishingStats({ error: error_.message });
    } finally {
      setLoadingPodStats(false);
    }
  };

  // Pod Membership handlers
  const handlePublishMembership = async () => {
    if (!membershipRecord.trim()) {
      toast.warning('Please enter membership record JSON data');
      return;
    }

    try {
      setPublishingMembership(true);
      setMembershipPublishResult(null);
      const record = JSON.parse(membershipRecord);
      const result = await mediacore.publishMembership(record);
      setMembershipPublishResult(result);
      setMembershipRecord('');
    } catch (error_) {
      setMembershipPublishResult({ error: error_.message });
    } finally {
      setPublishingMembership(false);
    }
  };

  const handleGetMembership = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setGettingMembership(true);
      setMembershipResult(null);
      const result = await mediacore.getMembership(
        membershipPodId,
        membershipPeerId,
      );
      setMembershipResult(result);
    } catch (error_) {
      setMembershipResult({ error: error_.message });
    } finally {
      setGettingMembership(false);
    }
  };

  const handleVerifyMembership = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setVerifyingMembership(true);
      setMembershipVerification(null);
      const result = await mediacore.verifyMembership(
        membershipPodId,
        membershipPeerId,
      );
      setMembershipVerification(result);
    } catch (error_) {
      setMembershipVerification({ error: error_.message });
    } finally {
      setVerifyingMembership(false);
    }
  };

  const handleBanMember = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    if (
      !confirm(
        `Are you sure you want to ban member "${membershipPeerId}" from pod "${membershipPodId}"?`,
      )
    ) {
      return;
    }

    try {
      setBanningMember(true);
      setBanResult(null);
      const result = await mediacore.banMember(
        membershipPodId,
        membershipPeerId,
        banReason || null,
      );
      setBanResult(result);
      setBanReason('');
    } catch (error_) {
      setBanResult({ error: error_.message });
    } finally {
      setBanningMember(false);
    }
  };

  const handleChangeRole = async () => {
    if (!membershipPodId.trim() || !membershipPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setChangingRole(true);
      setRoleChangeResult(null);
      const result = await mediacore.changeMemberRole(
        membershipPodId,
        membershipPeerId,
        newRole,
      );
      setRoleChangeResult(result);
    } catch (error_) {
      setRoleChangeResult({ error: error_.message });
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
    } catch (error_) {
      setMembershipStats({ error: error_.message });
    } finally {
      setLoadingMembershipStats(false);
    }
  };

  const handleCleanupMemberships = async () => {
    if (
      !confirm('Are you sure you want to cleanup expired membership records?')
    ) {
      return;
    }

    try {
      const result = await mediacore.cleanupExpiredMemberships();
      toast.success(
        `Cleanup completed: ${result.recordsCleaned} records cleaned, ${result.errorsEncountered} errors`,
      );
      // Reload stats to reflect changes
      await handleLoadMembershipStats();
    } catch (error_) {
      toast.error(`Failed to cleanup: ${error_.message}`);
    }
  };

  // Pod Membership Verification handlers
  const handleVerifyPodMembership = async () => {
    if (!verifyPodId.trim() || !verifyPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setVerifyingMembership(true);
      setMembershipVerificationResult(null);
      const result = await mediacore.verifyPodMembership(
        verifyPodId,
        verifyPeerId,
      );
      setMembershipVerificationResult(result);
    } catch (error_) {
      setMembershipVerificationResult({ error: error_.message });
    } finally {
      setVerifyingMembership(false);
    }
  };

  const handleVerifyMessage = async () => {
    if (!membershipMessageToVerify.trim()) {
      toast.warning('Please enter a message JSON');
      return;
    }

    try {
      setVerifyingMessage(true);
      setMessageVerificationResult(null);
      const message = JSON.parse(membershipMessageToVerify);
      const result = await mediacore.verifyPodMessage(message);
      setMessageVerificationResult(result);
    } catch (error_) {
      setMessageVerificationResult({ error: error_.message });
    } finally {
      setVerifyingMessage(false);
    }
  };

  const handleCheckRole = async () => {
    if (!roleCheckPodId.trim() || !roleCheckPeerId.trim()) {
      toast.warning('Please enter both Pod ID and Peer ID');
      return;
    }

    try {
      setCheckingRole(true);
      setRoleCheckResult(null);
      const hasRole = await mediacore.checkPodRole(
        roleCheckPodId,
        roleCheckPeerId,
        requiredRole,
      );
      setRoleCheckResult({ hasRole });
    } catch (error_) {
      setRoleCheckResult({ error: error_.message });
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
    } catch (error_) {
      setVerificationStats({ error: error_.message });
    } finally {
      setLoadingVerificationStats(false);
    }
  };

  // Pod Discovery handlers
  const handleRegisterPodForDiscovery = async () => {
    if (!podToRegister.trim()) {
      toast.warning('Please enter pod JSON data');
      return;
    }

    try {
      setRegisteringPod(true);
      setPodRegistrationResult(null);
      const pod = JSON.parse(podToRegister);
      const result = await mediacore.registerPodForDiscovery(pod);
      setPodRegistrationResult(result);
      setPodToRegister('');
    } catch (error_) {
      setPodRegistrationResult({ error: error_.message });
    } finally {
      setRegisteringPod(false);
    }
  };

  const handleUnregisterPodFromDiscovery = async () => {
    if (!podToUnregister.trim()) {
      toast.warning('Please enter a pod ID');
      return;
    }

    try {
      setUnregisteringPod(true);
      setPodUnregistrationResult(null);
      const result =
        await mediacore.unregisterPodFromDiscovery(podToUnregister);
      setPodUnregistrationResult(result);
      setPodToUnregister('');
    } catch (error_) {
      setPodUnregistrationResult({ error: error_.message });
    } finally {
      setUnregisteringPod(false);
    }
  };

  const handleDiscoverByName = async () => {
    if (!discoverByName.trim()) {
      toast.warning('Please enter a pod name');
      return;
    }

    try {
      setDiscoveringByName(true);
      setNameDiscoveryResult(null);
      const result = await mediacore.discoverPodsByName(discoverByName);
      setNameDiscoveryResult(result);
    } catch (error_) {
      setNameDiscoveryResult({ error: error_.message });
    } finally {
      setDiscoveringByName(false);
    }
  };

  const handleDiscoverByTag = async () => {
    if (!discoverByTag.trim()) {
      toast.warning('Please enter a tag');
      return;
    }

    try {
      setDiscoveringByTag(true);
      setTagDiscoveryResult(null);
      const result = await mediacore.discoverPodsByTag(discoverByTag);
      setTagDiscoveryResult(result);
    } catch (error_) {
      setTagDiscoveryResult({ error: error_.message });
    } finally {
      setDiscoveringByTag(false);
    }
  };

  const handleDiscoverByTags = async () => {
    if (!discoverTags.trim()) {
      toast.warning('Please enter tags (comma-separated)');
      return;
    }

    try {
      setDiscoveringByTags(true);
      setTagsDiscoveryResult(null);
      const tagList = discoverTags
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean);
      const result = await mediacore.discoverPodsByTags(tagList);
      setTagsDiscoveryResult(result);
    } catch (error_) {
      setTagsDiscoveryResult({ error: error_.message });
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
    } catch (error_) {
      setAllDiscoveryResult({ error: error_.message });
    } finally {
      setDiscoveringAll(false);
    }
  };

  const handleDiscoverByContent = async () => {
    if (!discoverByContent.trim()) {
      toast.warning('Please enter a content ID');
      return;
    }

    try {
      setDiscoveringByContent(true);
      setContentDiscoveryResult(null);
      const result = await mediacore.discoverPodsByContent(discoverByContent);
      setContentDiscoveryResult(result);
    } catch (error_) {
      setContentDiscoveryResult({ error: error_.message });
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
    } catch (error_) {
      setDiscoveryStats({ error: error_.message });
    } finally {
      setLoadingDiscoveryStats(false);
    }
  };

  const handleRefreshDiscovery = async () => {
    try {
      const result = await mediacore.refreshPodDiscovery();
      toast.success(
        `Discovery refresh completed: ${result.entriesRefreshed} refreshed, ${result.entriesExpired} expired`,
      );
      // Reload stats to reflect changes
      await handleLoadDiscoveryStats();
    } catch (error_) {
      toast.error(`Failed to refresh discovery: ${error_.message}`);
    }
  };

  // Pod Join/Leave handlers
  const handleRequestJoin = async () => {
    if (!joinRequestData.trim()) {
      toast.warning('Please enter join request JSON data');
      return;
    }

    try {
      setRequestingJoin(true);
      setJoinRequestResult(null);
      const joinRequest = JSON.parse(joinRequestData);
      const result = await mediacore.requestPodJoin(joinRequest);
      setJoinRequestResult(result);
      setJoinRequestData('');
    } catch (error_) {
      setJoinRequestResult({ error: error_.message });
    } finally {
      setRequestingJoin(false);
    }
  };

  const handleAcceptJoin = async () => {
    if (!acceptanceData.trim()) {
      toast.warning('Please enter acceptance JSON data');
      return;
    }

    try {
      setAcceptingJoin(true);
      setAcceptanceResult(null);
      const acceptance = JSON.parse(acceptanceData);
      const result = await mediacore.acceptPodJoin(acceptance);
      setAcceptanceResult(result);
      setAcceptanceData('');
    } catch (error_) {
      setAcceptanceResult({ error: error_.message });
    } finally {
      setAcceptingJoin(false);
    }
  };

  const handleRequestLeave = async () => {
    if (!leaveRequestData.trim()) {
      toast.warning('Please enter leave request JSON data');
      return;
    }

    try {
      setRequestingLeave(true);
      setLeaveRequestResult(null);
      const leaveRequest = JSON.parse(leaveRequestData);
      const result = await mediacore.requestPodLeave(leaveRequest);
      setLeaveRequestResult(result);
      setLeaveRequestData('');
    } catch (error_) {
      setLeaveRequestResult({ error: error_.message });
    } finally {
      setRequestingLeave(false);
    }
  };

  const handleAcceptLeave = async () => {
    if (!acceptanceData.trim()) {
      toast.warning('Please enter leave acceptance JSON data');
      return;
    }

    try {
      setAcceptingLeave(true);
      setLeaveAcceptanceResult(null);
      const acceptance = JSON.parse(acceptanceData);
      const result = await mediacore.acceptPodLeave(acceptance);
      setLeaveAcceptanceResult(result);
      setAcceptanceData('');
    } catch (error_) {
      setLeaveAcceptanceResult({ error: error_.message });
    } finally {
      setAcceptingLeave(false);
    }
  };

  const handleLoadPendingRequests = async () => {
    if (!pendingPodId.trim()) {
      toast.warning('Please enter a pod ID');
      return;
    }

    try {
      setLoadingPendingRequests(true);
      setPendingJoinRequests(null);
      setPendingLeaveRequests(null);

      const [joinRequests, leaveRequests] = await Promise.all([
        mediacore.getPendingJoinRequests(pendingPodId),
        mediacore.getPendingLeaveRequests(pendingPodId),
      ]);

      setPendingJoinRequests(joinRequests);
      setPendingLeaveRequests(leaveRequests);
    } catch (error_) {
      setPendingJoinRequests({ error: error_.message });
      setPendingLeaveRequests({ error: error_.message });
    } finally {
      setLoadingPendingRequests(false);
    }
  };

  // Pod Message Routing handlers
  const handleRouteMessage = async () => {
    if (!routeMessageData.trim()) {
      toast.warning('Please enter message JSON data');
      return;
    }

    try {
      setRoutingMessage(true);
      setRoutingResult(null);
      const message = JSON.parse(routeMessageData);
      const result = await mediacore.routePodMessage(message);
      setRoutingResult(result);
      setRouteMessageData('');
    } catch (error_) {
      setRoutingResult({ error: error_.message });
    } finally {
      setRoutingMessage(false);
    }
  };

  const handleRouteMessageToPeers = async () => {
    if (!routeToPeersMessage.trim() || !routeToPeersIds.trim()) {
      toast.warning('Please enter message JSON and target peer IDs');
      return;
    }

    try {
      setRoutingToPeers(true);
      setRoutingToPeersResult(null);
      const message = JSON.parse(routeToPeersMessage);
      const targetPeerIds = routeToPeersIds
        .split(',')
        .map((id) => id.trim())
        .filter(Boolean);
      const result = await mediacore.routePodMessageToPeers(
        message,
        targetPeerIds,
      );
      setRoutingToPeersResult(result);
      setRouteToPeersMessage('');
      setRouteToPeersIds('');
    } catch (error_) {
      setRoutingToPeersResult({ error: error_.message });
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
    } catch (error_) {
      setRoutingStats({ error: error_.message });
    } finally {
      setLoadingRoutingStats(false);
    }
  };

  const handleCheckMessageSeen = async () => {
    if (!checkMessageId.trim() || !checkPodId.trim()) {
      toast.warning('Please enter both message ID and pod ID');
      return;
    }

    try {
      setCheckingMessageSeen(true);
      setMessageSeenResult(null);
      const result = await mediacore.checkMessageSeen(
        checkMessageId,
        checkPodId,
      );
      setMessageSeenResult(result);
    } catch (error_) {
      setMessageSeenResult({ error: error_.message });
    } finally {
      setCheckingMessageSeen(false);
    }
  };

  const handleRegisterMessageSeen = async () => {
    if (!checkMessageId.trim() || !checkPodId.trim()) {
      toast.warning('Please enter both message ID and pod ID');
      return;
    }

    try {
      const result = await mediacore.registerMessageSeen(
        checkMessageId,
        checkPodId,
      );
      toast.success(
        `Message registered as seen: ${result.wasNewlyRegistered ? 'New' : 'Already known'}`,
      );
    } catch (error_) {
      toast.error(`Failed to register message: ${error_.message}`);
    }
  };

  const handleCleanupSeenMessages = async () => {
    try {
      const result = await mediacore.cleanupSeenMessages();
      toast.success(
        `Cleanup completed: ${result.messagesCleaned} messages cleaned, ${result.messagesRetained} retained`,
      );
      // Reload stats to reflect changes
      await handleLoadRoutingStats();
    } catch (error_) {
      toast.error(`Failed to cleanup: ${error_.message}`);
    }
  };

  // Pod Message Signing handlers
  const handleSignMessage = async () => {
    if (!messageToSign.trim() || !privateKeyForSigning.trim()) {
      toast.warning('Please enter message JSON and private key');
      return;
    }

    try {
      setSigningMessage(true);
      setSignedMessageResult(null);
      const message = JSON.parse(messageToSign);
      const result = await mediacore.signPodMessage(
        message,
        privateKeyForSigning,
      );
      setSignedMessageResult(result);
      setMessageToSign('');
    } catch (error_) {
      setSignedMessageResult({ error: error_.message });
    } finally {
      setSigningMessage(false);
    }
  };

  const handleVerifySignature = async () => {
    if (!messageToVerify.trim()) {
      toast.warning('Please enter message JSON to verify');
      return;
    }

    try {
      setVerifyingSignature(true);
      setVerificationResult(null);
      const message = JSON.parse(messageToVerify);
      const result = await mediacore.verifyPodMessageSignature(message);
      setVerificationResult(result);
    } catch (error_) {
      setVerificationResult({ error: error_.message });
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
    } catch (error_) {
      setGeneratedKeyPair({ error: error_.message });
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
    } catch (error_) {
      setSigningStats({ error: error_.message });
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
    } catch (error_) {
      setStorageStats({ error: error_.message });
      toast.error(`Failed to get storage stats: ${error_.message}`);
    } finally {
      setStorageStatsLoading(false);
    }
  };

  const handleCleanupMessages = async () => {
    try {
      setCleanupLoading(true);
      const thirtyDaysAgo = Date.now() - 30 * 24 * 60 * 60 * 1_000;
      const result = await mediacore.cleanupMessages(thirtyDaysAgo);
      toast.success(`Cleaned up ${result} old messages`);
      // Refresh stats after cleanup
      await handleGetStorageStats();
    } catch (error_) {
      toast.error(`Failed to cleanup messages: ${error_.message}`);
    } finally {
      setCleanupLoading(false);
    }
  };

  const handleRebuildSearchIndex = async () => {
    try {
      setRebuildIndexLoading(true);
      const result = await mediacore.rebuildSearchIndex();
      toast.success(
        result
          ? 'Search index rebuilt successfully'
          : 'Search index rebuild failed',
      );
    } catch (error_) {
      toast.error(`Failed to rebuild search index: ${error_.message}`);
    } finally {
      setRebuildIndexLoading(false);
    }
  };

  const handleVacuumDatabase = async () => {
    try {
      setVacuumLoading(true);
      const result = await mediacore.vacuumDatabase();
      toast.success(
        result
          ? 'Database vacuum completed successfully'
          : 'Database vacuum failed',
      );
    } catch (error_) {
      toast.error(`Failed to vacuum database: ${error_.message}`);
    } finally {
      setVacuumLoading(false);
    }
  };

  const handleSearchMessages = async () => {
    if (!searchQuery.trim()) return;

    try {
      setSearchLoading(true);
      setSearchResults(null);
      const result = await mediacore.searchMessages(
        'all',
        searchQuery,
        null,
        50,
      ); // Search all pods
      setSearchResults(result);
    } catch (error_) {
      setSearchResults([]);
      toast.error(`Failed to search messages: ${error_.message}`);
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
    } catch (error_) {
      setBackfillStats({ error: error_.message });
      toast.error(`Failed to get backfill stats: ${error_.message}`);
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
      toast.success(
        `Backfill sync completed: ${result.totalMessagesReceived} messages received`,
      );
      // Refresh stats
      await handleGetBackfillStats();
    } catch (error_) {
      toast.error(`Failed to sync pod backfill: ${error_.message}`);
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
    } catch (error_) {
      toast.error(`Failed to get last seen timestamps: ${error_.message}`);
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
    } catch (error_) {
      toast.error(`Failed to get channels: ${error_.message}`);
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
        kind: newChannelKind,
        name: newChannelName,
      };
      await mediacore.createChannel(channelPodId, channel);
      toast.success(`Channel "${newChannelName}" created successfully`);
      setNewChannelName('');
      // Refresh channels list
      await handleGetChannels();
    } catch (error_) {
      toast.error(`Failed to create channel: ${error_.message}`);
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
        channelId,
        kind: editingChannel.kind,
        name: editChannelName,
      };
      await mediacore.updateChannel(channelPodId, channelId, updatedChannel);
      toast.success(`Channel updated successfully`);
      setEditingChannel(null);
      setEditChannelName('');
      // Refresh channels list
      await handleGetChannels();
    } catch (error_) {
      toast.error(`Failed to update channel: ${error_.message}`);
    } finally {
      setUpdateChannelLoading(false);
    }
  };

  const handleDeleteChannel = async (channelId, channelName) => {
    if (
      !confirm(
        `Are you sure you want to delete the channel "${channelName}"? This action cannot be undone.`,
      )
    ) {
      return;
    }

    try {
      setDeleteChannelLoading(true);
      await mediacore.deleteChannel(channelPodId, channelId);
      toast.success(`Channel "${channelName}" deleted successfully`);
      // Refresh channels list
      await handleGetChannels();
    } catch (error_) {
      toast.error(`Failed to delete channel: ${error_.message}`);
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
      const result = await mediacore.validateContentIdForPod(contentId.trim());
      setContentValidation(result);

      // If valid, automatically fetch metadata
      if (result.isValid) {
        await handleGetContentMetadata();
      }
    } catch (error_) {
      setContentValidation({ error: error_.message, isValid: false });
      toast.error(`Failed to validate content ID: ${error_.message}`);
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
    } catch (error_) {
      toast.error(`Failed to get content metadata: ${error_.message}`);
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
      const results = await mediacore.searchContent(
        contentSearchQuery.trim(),
        null,
        10,
      );
      setContentSearchResults(results);
    } catch (error_) {
      toast.error(`Failed to search content: ${error_.message}`);
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
        channels: [
          {
            channelId: 'general',
            kind: 'General',
            name: 'General',
          },
        ],

        contentId: contentId.trim(),

        externalBindings: [],
        // Auto-generate
        name: newPodName.trim(),
        podId: '',
        tags: [],
        visibility: newPodVisibility,
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
    } catch (error_) {
      toast.error(`Failed to create pod: ${error_.message}`);
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
    if (
      !opinionPodId.trim() ||
      !opinionContentId.trim() ||
      !opinionVariantHash.trim()
    ) {
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
        note: opinionNote.trim(),
        score: opinionScore,
        senderPeerId: 'current-user',
        variantHash: opinionVariantHash.trim(), // Get from session when available
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
    } catch (error_) {
      toast.error(`Failed to publish opinion: ${error_.message}`);
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
      const result = await mediacore.getContentOpinions(
        opinionPodId.trim(),
        opinionContentId.trim(),
      );
      setOpinions(result);
    } catch (error_) {
      toast.error(`Failed to get opinions: ${error_.message}`);
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
      const stats = await mediacore.getOpinionStatistics(
        opinionPodId.trim(),
        opinionContentId.trim(),
      );
      setOpinionStatistics(stats);
    } catch (error_) {
      toast.error(`Failed to get opinion statistics: ${error_.message}`);
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
    } catch (error_) {
      toast.error(`Failed to refresh opinions: ${error_.message}`);
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
      const aggregated = await mediacore.getAggregatedOpinions(
        opinionPodId.trim(),
        opinionContentId.trim(),
      );
      setAggregatedOpinions(aggregated);
    } catch (error_) {
      toast.error(`Failed to get aggregated opinions: ${error_.message}`);
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
      const affinities = await mediacore.getMemberAffinities(
        opinionPodId.trim(),
      );
      setMemberAffinities(affinities);
    } catch (error_) {
      toast.error(`Failed to get member affinities: ${error_.message}`);
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
      const recommendations = await mediacore.getConsensusRecommendations(
        opinionPodId.trim(),
        opinionContentId.trim(),
      );
      setConsensusRecommendations(recommendations);
    } catch (error_) {
      toast.error(`Failed to get consensus recommendations: ${error_.message}`);
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
      const result = await mediacore.updateMemberAffinities(
        opinionPodId.trim(),
      );
      toast.success(`Updated affinities for ${result.membersUpdated} members`);

      // Refresh affinities display
      await handleGetMemberAffinities();
    } catch (error_) {
      toast.error(`Failed to update member affinities: ${error_.message}`);
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
    } catch (error_) {
      console.error('Failed to load conflict strategies:', error_);
    }
  };

  if (loading && !stats) {
    return (
      <Segment>
        <Loader
          active
          inline="centered"
        >
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

            {stats?.mappingsByDomain &&
              Object.keys(stats.mappingsByDomain).length > 0 && (
                <div style={{ marginTop: '1em' }}>
                  <Header as="h4">Mappings by Domain</Header>
                  <List horizontal>
                    {Object.entries(stats.mappingsByDomain).map(
                      ([domain, count]) => (
                        <List.Item key={domain}>
                          <Label>
                            {domain}
                            <Label.Detail>{count}</Label.Detail>
                          </Label>
                        </List.Item>
                      ),
                    )}
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
                    onChange={(e) => setExternalId(e.target.value)}
                    placeholder="e.g., mb:recording:12345-6789-..."
                    value={externalId}
                  />
                </Form.Field>
                <Form.Field>
                  <label>Content ID</label>
                  <Input
                    onChange={(e) => setDescriptorContentId(e.target.value)}
                    placeholder="e.g., content:mb:recording:12345-6789-..."
                    value={descriptorContentId}
                  />
                </Form.Field>
                <Button
                  disabled={
                    !externalId.trim() ||
                    !descriptorContentId.trim() ||
                    registering
                  }
                  loading={registering}
                  onClick={handleRegister}
                  primary
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
                    action={
                      <Button
                        disabled={!resolveId.trim() || resolving}
                        loading={resolving}
                        onClick={handleResolve}
                        primary
                      >
                        Resolve
                      </Button>
                    }
                    onChange={(e) => setResolveId(e.target.value)}
                    placeholder="Enter external ID to resolve..."
                    value={resolveId}
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
                        <strong>External ID:</strong>{' '}
                        {resolvedContent.externalId}
                        <br />
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
                    action={
                      <Button
                        disabled={!validateContentIdInput.trim() || validating}
                        loading={validating}
                        onClick={handleValidate}
                        primary
                      >
                        Validate
                      </Button>
                    }
                    onChange={(e) => setValidateContentIdInput(e.target.value)}
                    placeholder="e.g., content:audio:track:mb-12345"
                    value={validateContentIdInput}
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
                        <strong>Domain:</strong> {validatedContent.domain}
                        <br />
                        <strong>Type:</strong> {validatedContent.type}
                        <br />
                        <strong>ID:</strong> {validatedContent.id}
                        <br />
                        <strong>Audio:</strong>{' '}
                        {validatedContent.isAudio ? 'Yes' : 'No'} |
                        <strong>Video:</strong>{' '}
                        {validatedContent.isVideo ? 'Yes' : 'No'} |
                        <strong>Image:</strong>{' '}
                        {validatedContent.isImage ? 'Yes' : 'No'}
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
                      onChange={(e) => setDomain(e.target.value)}
                      placeholder="e.g., audio, video, image"
                      value={domain}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Type (optional)</label>
                    <Input
                      onChange={(e) => setType(e.target.value)}
                      placeholder="e.g., track, movie, photo"
                      value={type}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={!domain.trim() || searchingDomain}
                  loading={searchingDomain}
                  onClick={handleDomainSearch}
                  primary
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
                      <p>
                        <strong>
                          Found {domainResults.contentIds?.length || 0}{' '}
                          ContentIDs
                        </strong>
                      </p>
                      {domainResults.contentIds?.length > 0 && (
                        <List
                          divided
                          relaxed
                          style={{ maxHeight: '200px', overflow: 'auto' }}
                        >
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
                    onClick={() => fillExample(domainName, typeName)}
                    size="small"
                  >
                    {domainName}:{typeName}
                  </Button>
                )),
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
                      onChange={(e) => setTraverseContentId(e.target.value)}
                      placeholder="e.g., content:audio:track:mb-12345"
                      value={traverseContentId}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Link Type</label>
                    <Input
                      onChange={(e) => setTraverseLinkName(e.target.value)}
                      placeholder="e.g., album, artist, artwork"
                      value={traverseLinkName}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={
                    !traverseContentId.trim() ||
                    !traverseLinkName.trim() ||
                    traversing
                  }
                  loading={traversing}
                  onClick={handleTraverse}
                  primary
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
                      <p>
                        <strong>Traversal completed:</strong>{' '}
                        {traversalResults.completedTraversal ? 'Yes' : 'No'}
                      </p>
                      <p>
                        <strong>
                          Visited {traversalResults.visitedNodes?.length || 0}{' '}
                          nodes
                        </strong>
                      </p>
                      {traversalResults.visitedNodes?.length > 0 && (
                        <List
                          divided
                          relaxed
                          style={{ maxHeight: '150px', overflow: 'auto' }}
                        >
                          {traversalResults.visitedNodes.map((node, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header>{node.contentId}</List.Header>
                                <List.Description>
                                  {node.outgoingLinks?.length || 0} outgoing
                                  links
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
                    action={
                      <Button
                        disabled={!graphContentId.trim() || gettingGraph}
                        loading={gettingGraph}
                        onClick={handleGetGraph}
                        primary
                      >
                        Get Graph
                      </Button>
                    }
                    onChange={(e) => setGraphContentId(e.target.value)}
                    placeholder="Enter ContentID to get its graph"
                    value={graphContentId}
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
                      <p>
                        <strong>Root:</strong> {graphResults.rootContentId}
                      </p>
                      <p>
                        <strong>Nodes:</strong>{' '}
                        {graphResults.nodes?.length || 0}
                      </p>
                      <p>
                        <strong>Paths:</strong>{' '}
                        {graphResults.paths?.length || 0}
                      </p>
                      {graphResults.nodes?.length > 0 && (
                        <List
                          divided
                          relaxed
                          style={{ maxHeight: '150px', overflow: 'auto' }}
                        >
                          {graphResults.nodes.slice(0, 5).map((node, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header style={{ fontSize: '0.9em' }}>
                                  {node.contentId}
                                </List.Header>
                                <List.Description style={{ fontSize: '0.8em' }}>
                                  {node.outgoingLinks?.length || 0} outgoing,{' '}
                                  {node.incomingLinks?.length || 0} incoming
                                </List.Description>
                              </List.Content>
                            </List.Item>
                          ))}
                          {graphResults.nodes.length > 5 && (
                            <List.Item>
                              <List.Content>
                                <em>
                                  ... and {graphResults.nodes.length - 5} more
                                  nodes
                                </em>
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
                    action={
                      <Button
                        disabled={!inboundTargetId.trim() || findingInbound}
                        loading={findingInbound}
                        onClick={handleFindInbound}
                        primary
                      >
                        Find Links
                      </Button>
                    }
                    onChange={(e) => setInboundTargetId(e.target.value)}
                    placeholder="Find content that links to this ID"
                    value={inboundTargetId}
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
                      <p>
                        <strong>
                          Found {inboundResults.inboundLinks?.length || 0}{' '}
                          inbound links
                        </strong>
                      </p>
                      {inboundResults.inboundLinks?.length > 0 && (
                        <List
                          divided
                          relaxed
                          style={{ maxHeight: '150px', overflow: 'auto' }}
                        >
                          {inboundResults.inboundLinks.map((link, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <code style={{ fontSize: '0.9em' }}>
                                  {link}
                                </code>
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
                      onChange={(e, { value }) => setAudioAlgorithm(value)}
                      options={
                        supportedAlgorithms?.algorithms?.map((alg) => ({
                          key: alg,
                          text: alg,
                          value: alg,
                        })) || []
                      }
                      selection
                      value={audioAlgorithm}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Sample Rate (Hz)</label>
                    <Input
                      onChange={(e) => setSampleRate(e.target.value)}
                      type="number"
                      value={sampleRate}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Audio Samples (comma-separated floats)</label>
                  <TextArea
                    onChange={(e) => setAudioSamples(e.target.value)}
                    placeholder="0.1, -0.2, 0.3, ... (normalized -1.0 to 1.0)"
                    rows={3}
                    value={audioSamples}
                  />
                </Form.Field>
                <Button
                  disabled={!audioSamples.trim() || computingAudioHash}
                  loading={computingAudioHash}
                  onClick={handleComputeAudioHash}
                  primary
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
                        <strong>Algorithm:</strong> {audioHashResult.algorithm}
                        <br />
                        <strong>Hex Hash:</strong> {audioHashResult.hex}
                        <br />
                        <strong>Sample Count:</strong>{' '}
                        {audioSamples.split(',').filter((s) => s.trim()).length}
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
                      onChange={(e, { value }) => setImageAlgorithm(value)}
                      options={
                        supportedAlgorithms?.algorithms
                          ?.filter((alg) => alg !== 'ChromaPrint')
                          .map((alg) => ({
                            key: alg,
                            text: alg,
                            value: alg,
                          })) || []
                      }
                      selection
                      value={imageAlgorithm}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Dimensions</label>
                    <Input
                      onChange={(e) => {
                        const [w, h] = e.target.value
                          .split('x')
                          .map((s) => Number.parseInt(s.trim()));
                        if (!isNaN(w)) setImageWidth(w);
                        if (!isNaN(h)) setImageHeight(h);
                      }}
                      placeholder="Width x Height"
                      value={`${imageWidth}x${imageHeight}`}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Pixel Data (comma-separated bytes 0-255)</label>
                  <TextArea
                    onChange={(e) => setImagePixels(e.target.value)}
                    placeholder="255, 128, 64, ... (RGBA pixel data)"
                    rows={3}
                    value={imagePixels}
                  />
                </Form.Field>
                <Button
                  disabled={!imagePixels.trim() || computingImageHash}
                  loading={computingImageHash}
                  onClick={handleComputeImageHash}
                  primary
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
                        <strong>Algorithm:</strong> {imageHashResult.algorithm}
                        <br />
                        <strong>Hex Hash:</strong> {imageHashResult.hex}
                        <br />
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
                      onChange={(e) => setHashA(e.target.value)}
                      placeholder="First hash value (hexadecimal)"
                      value={hashA}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Hash B (hex)</label>
                    <Input
                      onChange={(e) => setHashB(e.target.value)}
                      placeholder="Second hash value (hexadecimal)"
                      value={hashB}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Similarity Threshold</label>
                    <Input
                      max="1"
                      min="0"
                      onChange={(e) => setSimilarityThreshold(e.target.value)}
                      step="0.1"
                      type="number"
                      value={similarityThreshold}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={
                    !hashA.trim() || !hashB.trim() || computingSimilarity
                  }
                  loading={computingSimilarity}
                  onClick={handleComputeSimilarity}
                  primary
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
                      <Message.Header>
                        Similarity Analysis Results
                      </Message.Header>
                      <p>
                        <strong>Hamming Distance:</strong>{' '}
                        {similarityResult.hammingDistance} bits
                        <br />
                        <strong>Similarity Score:</strong>{' '}
                        {(similarityResult.similarity * 100).toFixed(1)}%<br />
                        <strong>Are Similar:</strong>{' '}
                        {similarityResult.areSimilar ? 'Yes' : 'No'} (threshold:{' '}
                        {(similarityResult.threshold * 100).toFixed(1)}%)
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
                    onChange={(e) => setFindSimilarContentId(e.target.value)}
                    placeholder="ContentID to find matches for"
                    value={findSimilarContentId}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Min Confidence</label>
                    <Input
                      max="1"
                      min="0"
                      onChange={(e) =>
                        setFindSimilarMinConfidence(e.target.value)
                      }
                      step="0.1"
                      type="number"
                      value={findSimilarMinConfidence}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Max Results</label>
                    <Input
                      max="50"
                      min="1"
                      onChange={(e) => setFindSimilarMaxResults(e.target.value)}
                      type="number"
                      value={findSimilarMaxResults}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={
                    !findSimilarContentId.trim() || findingSimilarContent
                  }
                  loading={findingSimilarContent}
                  onClick={handleFindSimilarContent}
                  primary
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
                      <p>
                        <strong>Target:</strong>{' '}
                        {findSimilarResult.targetContentId}
                      </p>
                      <p>
                        <strong>
                          Searched {findSimilarResult.totalCandidates}{' '}
                          candidates
                        </strong>
                      </p>
                      <p>
                        <strong>
                          Found {findSimilarResult.matches?.length || 0} matches
                        </strong>
                      </p>
                      {findSimilarResult.matches?.length > 0 && (
                        <List
                          divided
                          relaxed
                          style={{ maxHeight: '200px', overflow: 'auto' }}
                        >
                          {findSimilarResult.matches.map((match, index) => (
                            <List.Item key={index}>
                              <List.Content>
                                <List.Header style={{ fontSize: '0.9em' }}>
                                  {match.candidateContentId}
                                </List.Header>
                                <List.Description>
                                  Confidence:{' '}
                                  {(match.confidence * 100).toFixed(1)}% |
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
                      onChange={(e) => setPerceptualContentIdA(e.target.value)}
                      placeholder="First ContentID"
                      value={perceptualContentIdA}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>ContentID B</label>
                    <Input
                      onChange={(e) => setPerceptualContentIdB(e.target.value)}
                      placeholder="Second ContentID"
                      value={perceptualContentIdB}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>Similarity Threshold</label>
                  <Input
                    max="1"
                    min="0"
                    onChange={(e) => setPerceptualThreshold(e.target.value)}
                    step="0.1"
                    type="number"
                    value={perceptualThreshold}
                  />
                </Form.Field>
                <Button
                  disabled={
                    !perceptualContentIdA.trim() ||
                    !perceptualContentIdB.trim() ||
                    computingPerceptualSimilarity
                  }
                  loading={computingPerceptualSimilarity}
                  onClick={handleComputePerceptualSimilarity}
                  primary
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
                        <strong>Content A:</strong>{' '}
                        {perceptualSimilarityResult.contentIdA}
                        <br />
                        <strong>Content B:</strong>{' '}
                        {perceptualSimilarityResult.contentIdB}
                        <br />
                        <strong>Similarity:</strong>{' '}
                        {(perceptualSimilarityResult.similarity * 100).toFixed(
                          1,
                        )}
                        %<br />
                        <strong>Are Similar:</strong>{' '}
                        {perceptualSimilarityResult.isSimilar ? 'Yes' : 'No'}{' '}
                        (threshold:{' '}
                        {(perceptualSimilarityResult.threshold * 100).toFixed(
                          1,
                        )}
                        %)
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
                Compare text strings using Levenshtein distance and phonetic
                matching
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Text A</label>
                    <Input
                      onChange={(e) => setTextSimilarityA(e.target.value)}
                      placeholder="First text string"
                      value={textSimilarityA}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Text B</label>
                    <Input
                      onChange={(e) => setTextSimilarityB(e.target.value)}
                      placeholder="Second text string"
                      value={textSimilarityB}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={
                    !textSimilarityA.trim() ||
                    !textSimilarityB.trim() ||
                    computingTextSimilarity
                  }
                  loading={computingTextSimilarity}
                  onClick={handleComputeTextSimilarity}
                  primary
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
                        <strong>Text A:</strong> "{textSimilarityResult.textA}"
                        <br />
                        <strong>Text B:</strong> "{textSimilarityResult.textB}"
                        <br />
                        <strong>Levenshtein Similarity:</strong>{' '}
                        {(
                          textSimilarityResult.levenshteinSimilarity * 100
                        ).toFixed(1)}
                        %<br />
                        <strong>Phonetic Similarity:</strong>{' '}
                        {(
                          textSimilarityResult.phoneticSimilarity * 100
                        ).toFixed(1)}
                        %<br />
                        <strong>Combined Similarity:</strong>{' '}
                        {(
                          textSimilarityResult.combinedSimilarity * 100
                        ).toFixed(1)}
                        %
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
                    onChange={(e) => setExportContentIds(e.target.value)}
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    rows={4}
                    value={exportContentIds}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    checked={includeLinks}
                    label="Include IPLD links"
                    onChange={(e, { checked }) => setIncludeLinks(checked)}
                  />
                </Form.Field>
                <Button
                  disabled={!exportContentIds.trim() || exportingMetadata}
                  loading={exportingMetadata}
                  onClick={handleExportMetadata}
                  primary
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
                        <strong>Version:</strong> {exportResult.version}
                        <br />
                        <strong>Entries:</strong>{' '}
                        {exportResult.metadata?.totalEntries || 0}
                        <br />
                        <strong>Links:</strong>{' '}
                        {exportResult.metadata?.totalLinks || 0}
                        <br />
                        <strong>Checksum:</strong>{' '}
                        {exportResult.metadata?.checksum?.slice(0, 16)}...
                      </p>
                      <details>
                        <summary>View Package JSON</summary>
                        <pre
                          style={{
                            fontSize: '0.8em',
                            maxHeight: '200px',
                            overflow: 'auto',
                          }}
                        >
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
                    onChange={(e, { value }) => setConflictStrategy(value)}
                    options={
                      availableStrategies?.strategies?.map((s) => ({
                        description: s.description,
                        key: s.strategy,
                        text: s.name,
                        value: s.strategy,
                      })) || []
                    }
                    selection
                    value={conflictStrategy}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    checked={dryRun}
                    label="Dry run (preview changes without applying)"
                    onChange={(e, { checked }) => setDryRun(checked)}
                  />
                </Form.Field>
                <Button
                  disabled={!importPackage.trim() || analyzingConflicts}
                  loading={analyzingConflicts}
                  onClick={handleAnalyzeConflicts}
                  secondary
                >
                  Analyze Conflicts
                </Button>
                <Button
                  disabled={!importPackage.trim() || importingMetadata}
                  loading={importingMetadata}
                  onClick={handleImportMetadata}
                  primary
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
                    onChange={(e) => setImportPackage(e.target.value)}
                    placeholder="Paste exported metadata package JSON here..."
                    rows={6}
                    value={importPackage}
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
                        <strong>Total Entries:</strong>{' '}
                        {conflictAnalysis.totalEntries}
                        <br />
                        <strong>Conflicting:</strong>{' '}
                        {conflictAnalysis.conflictingEntries}
                        <br />
                        <strong>Clean:</strong> {conflictAnalysis.cleanEntries}
                        <br />
                        <strong>Recommended Strategy:</strong>{' '}
                        {Object.entries(
                          conflictAnalysis.recommendedStrategies || {},
                        ).sort(([, a], [, b]) => b - a)[0]?.[0] || 'Merge'}
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
                      <Message.Header>
                        Import{' '}
                        {importResult.success
                          ? 'Successful'
                          : 'Completed with Issues'}
                      </Message.Header>
                      <p>
                        <strong>Processed:</strong>{' '}
                        {importResult.entriesProcessed}
                        <br />
                        <strong>Imported:</strong>{' '}
                        {importResult.entriesImported}
                        <br />
                        <strong>Skipped:</strong> {importResult.entriesSkipped}
                        <br />
                        <strong>Conflicts Resolved:</strong>{' '}
                        {importResult.conflictsResolved}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {importResult.duration?.TotalSeconds.toFixed(2)}s
                      </p>
                      {importResult.errors?.length > 0 && (
                        <details>
                          <summary>
                            Errors ({importResult.errors.length})
                          </summary>
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
                    onChange={(e) => setPublishContentId(e.target.value)}
                    placeholder="content:audio:track:mb-12345"
                    value={publishContentId}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Codec</label>
                    <Input
                      onChange={(e) => setPublishCodec(e.target.value)}
                      placeholder="mp3, flac, etc."
                      value={publishCodec}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Size (bytes)</label>
                    <Input
                      onChange={(e) => setPublishSize(e.target.value)}
                      type="number"
                      value={publishSize}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={!publishContentId.trim() || publishingDescriptor}
                  loading={publishingDescriptor}
                  onClick={handlePublishDescriptor}
                  primary
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
                        <strong>ContentID:</strong> {publishResult.contentId}
                        <br />
                        <strong>Version:</strong> {publishResult.version}
                        <br />
                        <strong>TTL:</strong> {publishResult.ttl?.totalMinutes}{' '}
                        minutes
                        <br />
                        <strong>Was Updated:</strong>{' '}
                        {publishResult.wasUpdated ? 'Yes' : 'No'}
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
                    onChange={(e) => setBatchContentIds(e.target.value)}
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    rows={6}
                    value={batchContentIds}
                  />
                </Form.Field>
                <Button
                  disabled={!batchContentIds.trim() || publishingBatch}
                  loading={publishingBatch}
                  onClick={handlePublishBatch}
                  primary
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
                        <strong>Total Requested:</strong>{' '}
                        {batchPublishResult.totalRequested}
                        <br />
                        <strong>Successfully Published:</strong>{' '}
                        {batchPublishResult.successfullyPublished}
                        <br />
                        <strong>Failed:</strong>{' '}
                        {batchPublishResult.failedToPublish}
                        <br />
                        <strong>Skipped:</strong> {batchPublishResult.skipped}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {batchPublishResult.totalDuration?.totalSeconds.toFixed(
                          2,
                        )}
                        s
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
                    onChange={(e) => setUpdateTargetId(e.target.value)}
                    placeholder="ContentID to update"
                    value={updateTargetId}
                  />
                </Form.Field>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>New Codec</label>
                    <Input
                      onChange={(e) => setUpdateCodec(e.target.value)}
                      placeholder="Leave empty to keep current"
                      value={updateCodec}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>New Size (bytes)</label>
                    <Input
                      onChange={(e) => setUpdateSize(e.target.value)}
                      placeholder="Leave empty to keep current"
                      value={updateSize}
                    />
                  </Form.Field>
                </Form.Group>
                <Form.Field>
                  <label>New Confidence (0.0-1.0)</label>
                  <Input
                    onChange={(e) => setUpdateConfidence(e.target.value)}
                    placeholder="Leave empty to keep current"
                    value={updateConfidence}
                  />
                </Form.Field>
                <Button
                  disabled={!updateTargetId.trim() || updatingDescriptor}
                  loading={updatingDescriptor}
                  onClick={handleUpdateDescriptor}
                  primary
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
                        <strong>ContentID:</strong> {updateResult.contentId}
                        <br />
                        <strong>Version:</strong> {updateResult.previousVersion}{' '}
                        → {updateResult.newVersion}
                        <br />
                        <strong>Updates Applied:</strong>{' '}
                        {updateResult.appliedUpdates?.join(', ') || 'none'}
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
                  disabled={republishing}
                  loading={republishing}
                  onClick={handleRepublishExpiring}
                >
                  Republish Expiring
                </Button>
                <Button
                  disabled={loadingStats}
                  loading={loadingStats}
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
                        <strong>Checked:</strong> {republishResult.totalChecked}
                        <br />
                        <strong>Republished:</strong>{' '}
                        {republishResult.republished}
                        <br />
                        <strong>Failed:</strong> {republishResult.failed}
                        <br />
                        <strong>Still Valid:</strong>{' '}
                        {republishResult.stillValid}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {republishResult.duration?.totalSeconds.toFixed(2)}s
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
                        <strong>Total Published:</strong>{' '}
                        {publishingStats.totalPublishedDescriptors}
                        <br />
                        <strong>Active Publications:</strong>{' '}
                        {publishingStats.activePublications}
                        <br />
                        <strong>Expiring Soon:</strong>{' '}
                        {publishingStats.expiringSoon}
                        <br />
                        <strong>Average TTL:</strong>{' '}
                        {publishingStats.averageTtlHours?.toFixed(1)} hours
                        <br />
                        <strong>Total Storage:</strong>{' '}
                        {(
                          publishingStats.totalStorageBytes /
                          1_024 /
                          1_024
                        )?.toFixed(1)}{' '}
                        MB
                      </p>
                      {publishingStats.publicationsByDomain &&
                        Object.keys(publishingStats.publicationsByDomain)
                          .length > 0 && (
                          <div style={{ marginTop: '0.5em' }}>
                            <strong>By Domain:</strong>
                            {Object.entries(
                              publishingStats.publicationsByDomain,
                            ).map(([domain, count]) => (
                              <Label
                                key={domain}
                                size="tiny"
                                style={{ margin: '0.1em' }}
                              >
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
                    onChange={(e) => setRetrieveContentId(e.target.value)}
                    placeholder="content:audio:track:mb-12345"
                    value={retrieveContentId}
                  />
                </Form.Field>
                <Form.Field>
                  <Checkbox
                    checked={bypassCache}
                    label="Bypass cache (force fresh retrieval)"
                    onChange={(e, { checked }) => setBypassCache(checked)}
                  />
                </Form.Field>
                <Button
                  disabled={!retrieveContentId.trim() || retrievingDescriptor}
                  loading={retrievingDescriptor}
                  onClick={handleRetrieveDescriptor}
                  primary
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
                      <p>
                        Content descriptor not found for:{' '}
                        {retrievalResult.contentId || retrieveContentId}
                      </p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Descriptor Retrieved</Message.Header>
                      <p>
                        <strong>ContentID:</strong>{' '}
                        {retrievalResult.descriptor?.contentId}
                        <br />
                        <strong>From Cache:</strong>{' '}
                        {retrievalResult.fromCache ? 'Yes' : 'No'}
                        <br />
                        <strong>Retrieved:</strong>{' '}
                        {new Date(retrievalResult.retrievedAt).toLocaleString()}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {retrievalResult.retrievalDuration?.totalMilliseconds.toFixed(
                          0,
                        )}
                        ms
                        <br />
                        <strong>Verified:</strong>{' '}
                        {retrievalResult.verification?.isValid ? 'Yes' : 'No'}
                        {retrievalResult.verification?.warnings?.length > 0 && (
                          <span> (with warnings)</span>
                        )}
                      </p>
                      <details>
                        <summary>View Descriptor JSON</summary>
                        <pre
                          style={{
                            fontSize: '0.8em',
                            maxHeight: '200px',
                            overflow: 'auto',
                          }}
                        >
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
                    onChange={(e) => setBatchRetrieveContentIds(e.target.value)}
                    placeholder="content:audio:track:mb-12345&#10;content:video:movie:imdb-tt0111161&#10;..."
                    rows={6}
                    value={batchRetrieveContentIds}
                  />
                </Form.Field>
                <Button
                  disabled={!batchRetrieveContentIds.trim() || retrievingBatch}
                  loading={retrievingBatch}
                  onClick={handleRetrieveBatch}
                  primary
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
                        <strong>Requested:</strong>{' '}
                        {batchRetrievalResult.requested}
                        <br />
                        <strong>Found:</strong> {batchRetrievalResult.found}
                        <br />
                        <strong>Failed:</strong> {batchRetrievalResult.failed}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {batchRetrievalResult.totalDuration?.totalSeconds.toFixed(
                          2,
                        )}
                        s
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
                      onChange={(e, { value }) => setQueryDomain(value)}
                      options={[
                        { key: 'audio', text: 'Audio', value: 'audio' },
                        { key: 'video', text: 'Video', value: 'video' },
                        { key: 'image', text: 'Image', value: 'image' },
                        { key: 'text', text: 'Text', value: 'text' },
                        {
                          key: 'application',
                          text: 'Application',
                          value: 'application',
                        },
                      ]}
                      selection
                      value={queryDomain}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Type (optional)</label>
                    <Input
                      onChange={(e) => setQueryType(e.target.value)}
                      placeholder="track, album, movie, etc."
                      value={queryType}
                    />
                  </Form.Field>
                  <Form.Field>
                    <label>Max Results</label>
                    <Input
                      max="1000"
                      min="1"
                      onChange={(e) => setQueryMaxResults(e.target.value)}
                      type="number"
                      value={queryMaxResults}
                    />
                  </Form.Field>
                </Form.Group>
                <Button
                  disabled={!queryDomain.trim() || queryingDescriptors}
                  loading={queryingDescriptors}
                  onClick={handleQueryDescriptors}
                  primary
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
                        {queryResult.type && (
                          <span>
                            {' '}
                            | <strong>Type:</strong> {queryResult.type}
                          </span>
                        )}
                        <br />
                        <strong>Found:</strong> {queryResult.totalFound}
                        <br />
                        <strong>Query Time:</strong>{' '}
                        {queryResult.queryDuration?.totalMilliseconds.toFixed(
                          0,
                        )}
                        ms
                        <br />
                        <strong>Has More:</strong>{' '}
                        {queryResult.hasMoreResults ? 'Yes' : 'No'}
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
                    onChange={(e) => setVerifyDescriptor(e.target.value)}
                    placeholder="Paste descriptor JSON to verify..."
                    rows={8}
                    value={verifyDescriptor}
                  />
                </Form.Field>
                <Button
                  disabled={!verifyDescriptor.trim() || verifyingDescriptor}
                  loading={verifyingDescriptor}
                  onClick={handleVerifyDescriptor}
                  primary
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
                    <Message
                      success={descriptorVerificationResult.isValid}
                      warning={!descriptorVerificationResult.isValid}
                    >
                      <Message.Header>
                        Verification Result:{' '}
                        {descriptorVerificationResult.isValid
                          ? 'Valid'
                          : 'Invalid'}
                      </Message.Header>
                      <p>
                        <strong>Signature Valid:</strong>{' '}
                        {descriptorVerificationResult.signatureValid
                          ? 'Yes'
                          : 'No'}
                        <br />
                        <strong>Freshness Valid:</strong>{' '}
                        {descriptorVerificationResult.freshnessValid
                          ? 'Yes'
                          : 'No'}
                        <br />
                        <strong>Age:</strong>{' '}
                        {descriptorVerificationResult.age?.totalMinutes.toFixed(
                          1,
                        )}{' '}
                        minutes
                      </p>
                      {descriptorVerificationResult.warnings?.length > 0 && (
                        <div>
                          <strong>Warnings:</strong>
                          <List bulleted>
                            {descriptorVerificationResult.warnings.map(
                              (warning, index) => (
                                <List.Item key={index}>{warning}</List.Item>
                              ),
                            )}
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
                  disabled={loadingRetrievalStats}
                  loading={loadingRetrievalStats}
                  onClick={handleLoadRetrievalStats}
                >
                  Load Stats
                </Button>
                <Button onClick={handleClearRetrievalCache}>Clear Cache</Button>
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
                        <strong>Total Retrievals:</strong>{' '}
                        {retrievalStats.totalRetrievals}
                        <br />
                        <strong>Cache Hits:</strong> {retrievalStats.cacheHits}
                        <br />
                        <strong>Cache Misses:</strong>{' '}
                        {retrievalStats.cacheMisses}
                        <br />
                        <strong>Hit Ratio:</strong>{' '}
                        {(retrievalStats.cacheHitRatio * 100).toFixed(1)}%<br />
                        <strong>Avg Retrieval Time:</strong>{' '}
                        {retrievalStats.averageRetrievalTime?.totalMilliseconds.toFixed(
                          0,
                        )}
                        ms
                        <br />
                        <strong>Active Cache Entries:</strong>{' '}
                        {retrievalStats.activeCacheEntries}
                        <br />
                        <strong>Cache Size:</strong>{' '}
                        {(retrievalStats.cacheSizeBytes / 1_024).toFixed(1)} KB
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
                Comprehensive overview of all MediaCore system performance and
                usage metrics
              </Card.Description>
            </Card.Content>
            <Card.Content>
              <Button.Group fluid>
                <Button
                  disabled={loadingDashboard}
                  loading={loadingDashboard}
                  onClick={handleLoadMediaCoreDashboard}
                  primary
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
                      <strong>Uptime:</strong>{' '}
                      {mediaCoreDashboard.uptime
                        ? `${Math.floor(mediaCoreDashboard.uptime.totalHours)}h ${mediaCoreDashboard.uptime.minutes}m`
                        : 'N/A'}
                      <br />
                      <strong>Last Updated:</strong>{' '}
                      {mediaCoreDashboard.timestamp
                        ? new Date(
                            mediaCoreDashboard.timestamp,
                          ).toLocaleString()
                        : 'N/A'}
                    </p>
                  </Message>

                  {/* System Resources */}
                  {mediaCoreDashboard.systemResources && (
                    <Message>
                      <Message.Header>System Resources</Message.Header>
                      <p>
                        <strong>Working Set:</strong>{' '}
                        {(
                          mediaCoreDashboard.systemResources.workingSetBytes /
                          1_024 /
                          1_024
                        ).toFixed(1)}{' '}
                        MB
                        <br />
                        <strong>Private Memory:</strong>{' '}
                        {(
                          mediaCoreDashboard.systemResources
                            .privateMemoryBytes /
                          1_024 /
                          1_024
                        ).toFixed(1)}{' '}
                        MB
                        <br />
                        <strong>GC Memory:</strong>{' '}
                        {(
                          mediaCoreDashboard.systemResources
                            .gcTotalMemoryBytes /
                          1_024 /
                          1_024
                        ).toFixed(1)}{' '}
                        MB
                        <br />
                        <strong>Thread Count:</strong>{' '}
                        {mediaCoreDashboard.systemResources.threadCount}
                      </p>
                    </Message>
                  )}
                </div>
              )}

              {/* Error Display */}
              {mediaCoreDashboard?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingRegistryStats}
                fluid
                loading={loadingRegistryStats}
                onClick={handleLoadContentRegistryStats}
              >
                Load Registry Stats
              </Button>

              {contentRegistryStats && !contentRegistryStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message success>
                    <Message.Header>Registry Overview</Message.Header>
                    <p>
                      <strong>Total Mappings:</strong>{' '}
                      {contentRegistryStats.totalMappings}
                      <br />
                      <strong>Domains:</strong>{' '}
                      {contentRegistryStats.totalDomains}
                      <br />
                      <strong>Avg Mappings/Domain:</strong>{' '}
                      {contentRegistryStats.averageMappingsPerDomain.toFixed(1)}
                    </p>
                    {contentRegistryStats.mappingsByDomain &&
                      Object.keys(contentRegistryStats.mappingsByDomain)
                        .length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Mappings by Domain:</strong>
                          {Object.entries(
                            contentRegistryStats.mappingsByDomain,
                          ).map(([domain, count]) => (
                            <Label
                              key={domain}
                              size="tiny"
                              style={{ margin: '0.1em' }}
                            >
                              {domain}: {count}
                            </Label>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {contentRegistryStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingDescriptorStats}
                fluid
                loading={loadingDescriptorStats}
                onClick={handleLoadDescriptorStats}
              >
                Load Descriptor Stats
              </Button>

              {descriptorStats && !descriptorStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Cache Performance</Message.Header>
                    <p>
                      <strong>Total Retrievals:</strong>{' '}
                      {descriptorStats.totalRetrievals}
                      <br />
                      <strong>Cache Hits:</strong> {descriptorStats.cacheHits}
                      <br />
                      <strong>Cache Misses:</strong>{' '}
                      {descriptorStats.cacheMisses}
                      <br />
                      <strong>Hit Ratio:</strong>{' '}
                      {(descriptorStats.cacheHitRatio * 100).toFixed(1)}%<br />
                      <strong>Avg Retrieval Time:</strong>{' '}
                      {descriptorStats.averageRetrievalTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                      <br />
                      <strong>Active Cache Entries:</strong>{' '}
                      {descriptorStats.activeCacheEntries}
                      <br />
                      <strong>Cache Size:</strong>{' '}
                      {(descriptorStats.cacheSizeBytes / 1_024).toFixed(1)} KB
                    </p>
                  </Message>
                </div>
              )}

              {descriptorStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingFuzzyStats}
                fluid
                loading={loadingFuzzyStats}
                onClick={handleLoadFuzzyMatchingStats}
              >
                Load Fuzzy Stats
              </Button>

              {fuzzyMatchingStats && !fuzzyMatchingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Matching Performance</Message.Header>
                    <p>
                      <strong>Total Matches:</strong>{' '}
                      {fuzzyMatchingStats.totalMatches}
                      <br />
                      <strong>Success Rate:</strong>{' '}
                      {(fuzzyMatchingStats.successRate * 100).toFixed(1)}%<br />
                      <strong>Avg Confidence:</strong>{' '}
                      {(
                        fuzzyMatchingStats.averageConfidenceScore * 100
                      ).toFixed(1)}
                      %<br />
                      <strong>Avg Match Time:</strong>{' '}
                      {fuzzyMatchingStats.averageMatchingTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                    </p>
                    {fuzzyMatchingStats.accuracyByAlgorithm &&
                      Object.keys(fuzzyMatchingStats.accuracyByAlgorithm)
                        .length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Algorithm Accuracy:</strong>
                          {Object.entries(
                            fuzzyMatchingStats.accuracyByAlgorithm,
                          ).map(([algorithm, stats]) => (
                            <div
                              key={algorithm}
                              style={{ margin: '0.2em 0' }}
                            >
                              <small>
                                {algorithm}: F1={stats.f1Score.toFixed(2)},
                                Precision={stats.precision.toFixed(2)}
                              </small>
                            </div>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {fuzzyMatchingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingPerceptualStats}
                fluid
                loading={loadingPerceptualStats}
                onClick={handleLoadPerceptualHashingStats}
              >
                Load Hashing Stats
              </Button>

              {perceptualHashingStats && !perceptualHashingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Hashing Performance</Message.Header>
                    <p>
                      <strong>Total Hashes:</strong>{' '}
                      {perceptualHashingStats.totalHashesComputed}
                      <br />
                      <strong>Avg Computation Time:</strong>{' '}
                      {perceptualHashingStats.averageComputationTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                      <br />
                      <strong>Overall Accuracy:</strong>{' '}
                      {(perceptualHashingStats.overallAccuracy * 100).toFixed(
                        1,
                      )}
                      %<br />
                      <strong>Duplicates Detected:</strong>{' '}
                      {perceptualHashingStats.duplicateHashesDetected}
                    </p>
                    {perceptualHashingStats.statsByAlgorithm &&
                      Object.keys(perceptualHashingStats.statsByAlgorithm)
                        .length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Algorithm Breakdown:</strong>
                          {Object.entries(
                            perceptualHashingStats.statsByAlgorithm,
                          ).map(([algorithm, stats]) => (
                            <div
                              key={algorithm}
                              style={{ margin: '0.2em 0' }}
                            >
                              <small>
                                {algorithm}: {stats.hashesComputed} hashes,{' '}
                                {stats.averageTime.totalMilliseconds.toFixed(0)}
                                ms avg, {stats.accuracy.toFixed(2)} accuracy
                              </small>
                            </div>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {perceptualHashingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingIpldStats}
                fluid
                loading={loadingIpldStats}
                onClick={handleLoadIpldMappingStats}
              >
                Load IPLD Stats
              </Button>

              {ipldMappingStats && !ipldMappingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Graph Statistics</Message.Header>
                    <p>
                      <strong>Total Links:</strong>{' '}
                      {ipldMappingStats.totalLinks}
                      <br />
                      <strong>Total Nodes:</strong>{' '}
                      {ipldMappingStats.totalNodes}
                      <br />
                      <strong>Total Graphs:</strong>{' '}
                      {ipldMappingStats.totalGraphs}
                      <br />
                      <strong>Connectivity Ratio:</strong>{' '}
                      {(ipldMappingStats.graphConnectivityRatio * 100).toFixed(
                        1,
                      )}
                      %<br />
                      <strong>Broken Links:</strong>{' '}
                      {ipldMappingStats.brokenLinksDetected}
                      <br />
                      <strong>Avg Traversal Time:</strong>{' '}
                      {ipldMappingStats.averageTraversalTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                    </p>
                  </Message>
                </div>
              )}

              {ipldMappingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingPortabilityStats}
                fluid
                loading={loadingPortabilityStats}
                onClick={handleLoadMetadataPortabilityStats}
              >
                Load Portability Stats
              </Button>

              {metadataPortabilityStats && !metadataPortabilityStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Portability Metrics</Message.Header>
                    <p>
                      <strong>Total Exports:</strong>{' '}
                      {metadataPortabilityStats.totalExports}
                      <br />
                      <strong>Total Imports:</strong>{' '}
                      {metadataPortabilityStats.totalImports}
                      <br />
                      <strong>Import Success Rate:</strong>{' '}
                      {(
                        metadataPortabilityStats.importSuccessRate * 100
                      ).toFixed(1)}
                      %<br />
                      <strong>Data Transferred:</strong>{' '}
                      {(
                        metadataPortabilityStats.totalDataTransferred / 1_024
                      ).toFixed(1)}{' '}
                      KB
                      <br />
                      <strong>Avg Export Time:</strong>{' '}
                      {metadataPortabilityStats.averageExportTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                      <br />
                      <strong>Avg Import Time:</strong>{' '}
                      {metadataPortabilityStats.averageImportTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                    </p>
                  </Message>
                </div>
              )}

              {metadataPortabilityStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                disabled={loadingPublishingStats}
                fluid
                loading={loadingPublishingStats}
                onClick={handleLoadContentPublishingStats}
              >
                Load Publishing Stats
              </Button>

              {contentPublishingStats && !contentPublishingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Publishing Overview</Message.Header>
                    <p>
                      <strong>Total Published:</strong>{' '}
                      {contentPublishingStats.totalPublished}
                      <br />
                      <strong>Active Publications:</strong>{' '}
                      {contentPublishingStats.activePublications}
                      <br />
                      <strong>Expired Publications:</strong>{' '}
                      {contentPublishingStats.expiredPublications}
                      <br />
                      <strong>Success Rate:</strong>{' '}
                      {(
                        contentPublishingStats.publicationSuccessRate * 100
                      ).toFixed(1)}
                      %<br />
                      <strong>Republished:</strong>{' '}
                      {contentPublishingStats.republishedDescriptors}
                      <br />
                      <strong>Failed:</strong>{' '}
                      {contentPublishingStats.failedPublications}
                      <br />
                      <strong>Avg Publish Time:</strong>{' '}
                      {contentPublishingStats.averagePublishTime?.totalMilliseconds.toFixed(
                        0,
                      )}
                      ms
                    </p>
                    {contentPublishingStats.publicationsByDomain &&
                      Object.keys(contentPublishingStats.publicationsByDomain)
                        .length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Publications by Domain:</strong>
                          {Object.entries(
                            contentPublishingStats.publicationsByDomain,
                          ).map(([domain, count]) => (
                            <Label
                              key={domain}
                              size="tiny"
                              style={{ margin: '0.1em' }}
                            >
                              {domain}: {count}
                            </Label>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {contentPublishingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                Publish and manage pod metadata on the decentralized DHT for
                discovery
              </Card.Description>
            </Card.Content>

            {/* Publish Pod */}
            <Card.Content>
              <Header size="small">Publish Pod to DHT</Header>
              <Form>
                <Form.TextArea
                  label="Pod JSON"
                  onChange={(e) => setPodToPublish(e.target.value)}
                  placeholder='{"id": {"value": "pod:artist:mb:daft-punk-hash"}, "displayName": "Daft Punk Fans", "visibility": "Listed", "focusType": "ContentId", "focusContentId": {"domain": "audio", "type": "artist", "id": "daft-punk-hash"}, "tags": ["electronic", "french-house"], "createdAt": "2024-01-01T00:00:00Z", "createdBy": "alice", "metadata": {"description": "A community for Daft Punk fans", "memberCount": 150}}'
                  rows={6}
                  value={podToPublish}
                />
                <Button
                  disabled={publishingPod || !podToPublish.trim()}
                  loading={publishingPod}
                  onClick={handlePublishPod}
                  primary
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
                      <Message.Header>
                        Pod Published Successfully
                      </Message.Header>
                      <p>
                        <strong>Pod ID:</strong>{' '}
                        {podPublishingResult.podId?.value ||
                          podPublishingResult.podId}
                        <br />
                        <strong>DHT Key:</strong> {podPublishingResult.dhtKey}
                        <br />
                        <strong>Published:</strong>{' '}
                        {new Date(
                          podPublishingResult.publishedAt,
                        ).toLocaleString()}
                        <br />
                        <strong>Expires:</strong>{' '}
                        {new Date(
                          podPublishingResult.expiresAt,
                        ).toLocaleString()}
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
                      onChange={(e) => setPodMetadataToRetrieve(e.target.value)}
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={podMetadataToRetrieve}
                    />
                    <Button
                      disabled={
                        retrievingPodMetadata || !podMetadataToRetrieve.trim()
                      }
                      fluid
                      loading={retrievingPodMetadata}
                      onClick={handleRetrievePodMetadata}
                    >
                      Retrieve Metadata
                    </Button>
                  </Form>

                  {podMetadataResult && (
                    <div style={{ marginTop: '1em' }}>
                      {podMetadataResult.error ? (
                        <Message error>
                          <p>
                            Failed to retrieve metadata:{' '}
                            {podMetadataResult.error}
                          </p>
                        </Message>
                      ) : podMetadataResult.found ? (
                        <Message success>
                          <Message.Header>
                            Pod Metadata Retrieved
                          </Message.Header>
                          <p>
                            <strong>Pod ID:</strong>{' '}
                            {podMetadataResult.podId?.value ||
                              podMetadataResult.podId}
                            <br />
                            <strong>Signature Valid:</strong>{' '}
                            {podMetadataResult.isValidSignature ? 'Yes' : 'No'}
                            <br />
                            <strong>Retrieved:</strong>{' '}
                            {new Date(
                              podMetadataResult.retrievedAt,
                            ).toLocaleString()}
                            <br />
                            <strong>Expires:</strong>{' '}
                            {new Date(
                              podMetadataResult.expiresAt,
                            ).toLocaleString()}
                            <br />
                            <strong>Display Name:</strong>{' '}
                            {podMetadataResult.publishedPod?.displayName}
                            <br />
                            <strong>Members:</strong>{' '}
                            {podMetadataResult.publishedPod?.metadata
                              ?.memberCount || 'Unknown'}
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
                      onChange={(e) => setPodToUnpublish(e.target.value)}
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={podToUnpublish}
                    />
                    <Button
                      color="red"
                      disabled={unpublishingPod || !podToUnpublish.trim()}
                      fluid
                      loading={unpublishingPod}
                      onClick={handleUnpublishPod}
                    >
                      Unpublish Pod
                    </Button>
                  </Form>

                  {podUnpublishResult && (
                    <div style={{ marginTop: '1em' }}>
                      {podUnpublishResult.error ? (
                        <Message error>
                          <p>
                            Failed to unpublish pod: {podUnpublishResult.error}
                          </p>
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
                  disabled={loadingPodStats}
                  loading={loadingPodStats}
                  onClick={handleLoadPodPublishingStats}
                  primary
                >
                  Load Pod Publishing Stats
                </Button>
              </Button.Group>

              {podPublishingStats && !podPublishingStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Pod Publishing Statistics</Message.Header>
                    <p>
                      <strong>Total Published:</strong>{' '}
                      {podPublishingStats.totalPublished}
                      <br />
                      <strong>Active Publications:</strong>{' '}
                      {podPublishingStats.activePublications}
                      <br />
                      <strong>Expired Publications:</strong>{' '}
                      {podPublishingStats.expiredPublications}
                      <br />
                      <strong>Failed Publications:</strong>{' '}
                      {podPublishingStats.failedPublications}
                      <br />
                      <strong>Avg Publish Time:</strong>{' '}
                      {podPublishingStats.averagePublishTime
                        ? `${podPublishingStats.averagePublishTime.totalMilliseconds.toFixed(0)}ms`
                        : 'N/A'}
                      <br />
                      <strong>Last Operation:</strong>{' '}
                      {podPublishingStats.lastPublishOperation
                        ? new Date(
                            podPublishingStats.lastPublishOperation,
                          ).toLocaleString()
                        : 'Never'}
                    </p>
                    {podPublishingStats.publicationsByVisibility &&
                      Object.keys(podPublishingStats.publicationsByVisibility)
                        .length > 0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Publications by Visibility:</strong>
                          {Object.entries(
                            podPublishingStats.publicationsByVisibility,
                          ).map(([visibility, count]) => (
                            <Label
                              key={visibility}
                              size="tiny"
                              style={{ margin: '0.1em' }}
                            >
                              {visibility}: {count}
                            </Label>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {podPublishingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
                  <p>
                    Failed to load pod publishing stats:{' '}
                    {podPublishingStats.error}
                  </p>
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
                Manage signed membership records in DHT with role-based access
                control
              </Card.Description>
            </Card.Content>

            {/* Publish Membership */}
            <Card.Content>
              <Header size="small">Publish Membership Record</Header>
              <Form>
                <Form.TextArea
                  label="Membership Record JSON"
                  onChange={(e) => setMembershipRecord(e.target.value)}
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "role": "member", "isBanned": false, "publicKey": "base64-ed25519-key", "joinedAt": "2024-01-01T00:00:00Z"}'
                  rows={4}
                  value={membershipRecord}
                />
                <Button
                  disabled={publishingMembership || !membershipRecord.trim()}
                  loading={publishingMembership}
                  onClick={handlePublishMembership}
                  primary
                >
                  Publish Membership
                </Button>
              </Form>

              {membershipPublishResult && (
                <div style={{ marginTop: '1em' }}>
                  {membershipPublishResult.error ? (
                    <Message error>
                      <p>
                        Failed to publish membership:{' '}
                        {membershipPublishResult.error}
                      </p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>
                        Membership Published Successfully
                      </Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {membershipPublishResult.podId}
                        <br />
                        <strong>Peer ID:</strong>{' '}
                        {membershipPublishResult.peerId}
                        <br />
                        <strong>DHT Key:</strong>{' '}
                        {membershipPublishResult.dhtKey}
                        <br />
                        <strong>Published:</strong>{' '}
                        {new Date(
                          membershipPublishResult.publishedAt,
                        ).toLocaleString()}
                        <br />
                        <strong>Expires:</strong>{' '}
                        {new Date(
                          membershipPublishResult.expiresAt,
                        ).toLocaleString()}
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
                      onChange={(e) => setMembershipPodId(e.target.value)}
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={membershipPodId}
                    />
                    <Form.Input
                      label="Peer ID"
                      onChange={(e) => setMembershipPeerId(e.target.value)}
                      placeholder="alice"
                      value={membershipPeerId}
                    />
                    <Button.Group fluid>
                      <Button
                        disabled={
                          gettingMembership ||
                          !membershipPodId.trim() ||
                          !membershipPeerId.trim()
                        }
                        loading={gettingMembership}
                        onClick={handleGetMembership}
                      >
                        Get Membership
                      </Button>
                      <Button
                        disabled={
                          verifyingMembership ||
                          !membershipPodId.trim() ||
                          !membershipPeerId.trim()
                        }
                        loading={verifyingMembership}
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
                          <p>
                            Failed to get membership: {membershipResult.error}
                          </p>
                        </Message>
                      ) : membershipResult.found ? (
                        <Message success>
                          <Message.Header>Membership Found</Message.Header>
                          <p>
                            <strong>Pod ID:</strong> {membershipResult.podId}
                            <br />
                            <strong>Peer ID:</strong> {membershipResult.peerId}
                            <br />
                            <strong>Role:</strong>{' '}
                            {membershipResult.signedRecord?.membership?.role}
                            <br />
                            <strong>Banned:</strong>{' '}
                            {membershipResult.signedRecord?.membership?.isBanned
                              ? 'Yes'
                              : 'No'}
                            <br />
                            <strong>Signature Valid:</strong>{' '}
                            {membershipResult.isValidSignature ? 'Yes' : 'No'}
                            <br />
                            <strong>Joined:</strong>{' '}
                            {membershipResult.signedRecord?.membership?.joinedAt
                              ? new Date(
                                  membershipResult.signedRecord.membership.joinedAt,
                                ).toLocaleString()
                              : 'Unknown'}
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
                          <p>
                            Failed to verify membership:{' '}
                            {membershipVerification.error}
                          </p>
                        </Message>
                      ) : (
                        <Message info>
                          <Message.Header>
                            Membership Verification
                          </Message.Header>
                          <p>
                            <strong>Valid Member:</strong>{' '}
                            {membershipVerification.isValidMember
                              ? 'Yes'
                              : 'No'}
                            <br />
                            <strong>Role:</strong>{' '}
                            {membershipVerification.role || 'None'}
                            <br />
                            <strong>Banned:</strong>{' '}
                            {membershipVerification.isBanned ? 'Yes' : 'No'}
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
                      onChange={(e) => setBanReason(e.target.value)}
                      placeholder="Violation of community rules"
                      value={banReason}
                    />
                    <Button
                      color="red"
                      disabled={
                        banningMember ||
                        !membershipPodId.trim() ||
                        !membershipPeerId.trim()
                      }
                      fluid
                      loading={banningMember}
                      onClick={handleBanMember}
                    >
                      Ban Member
                    </Button>
                  </Form>

                  {/* Change Role */}
                  <Form>
                    <Form.Select
                      label="New Role"
                      onChange={(e, { value }) => setNewRole(value)}
                      options={[
                        { key: 'member', text: 'Member', value: 'member' },
                        { key: 'mod', text: 'Moderator', value: 'mod' },
                        { key: 'owner', text: 'Owner', value: 'owner' },
                      ]}
                      value={newRole}
                    />
                    <Button
                      color="blue"
                      disabled={
                        changingRole ||
                        !membershipPodId.trim() ||
                        !membershipPeerId.trim()
                      }
                      fluid
                      loading={changingRole}
                      onClick={handleChangeRole}
                    >
                      Change Role
                    </Button>
                  </Form>

                  {/* Management Results */}
                  {banResult && (
                    <Message
                      style={{ marginTop: '1em' }}
                      success
                    >
                      <p>Member banned successfully</p>
                    </Message>
                  )}

                  {roleChangeResult && (
                    <Message
                      style={{ marginTop: '1em' }}
                      success
                    >
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
                  disabled={loadingMembershipStats}
                  loading={loadingMembershipStats}
                  onClick={handleLoadMembershipStats}
                  primary
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
                      <strong>Total Memberships:</strong>{' '}
                      {membershipStats.totalMemberships}
                      <br />
                      <strong>Active Memberships:</strong>{' '}
                      {membershipStats.activeMemberships}
                      <br />
                      <strong>Banned Memberships:</strong>{' '}
                      {membershipStats.bannedMemberships}
                      <br />
                      <strong>Expired Memberships:</strong>{' '}
                      {membershipStats.expiredMemberships}
                      <br />
                      <strong>Last Operation:</strong>{' '}
                      {membershipStats.lastOperation
                        ? new Date(
                            membershipStats.lastOperation,
                          ).toLocaleString()
                        : 'Never'}
                    </p>
                    {membershipStats.membershipsByRole &&
                      Object.keys(membershipStats.membershipsByRole).length >
                        0 && (
                        <div style={{ marginTop: '0.5em' }}>
                          <strong>Memberships by Role:</strong>
                          {Object.entries(
                            membershipStats.membershipsByRole,
                          ).map(([role, count]) => (
                            <Label
                              key={role}
                              size="tiny"
                              style={{ margin: '0.1em' }}
                            >
                              {role}: {count}
                            </Label>
                          ))}
                        </div>
                      )}
                  </Message>
                </div>
              )}

              {membershipStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
                  <p>
                    Failed to load membership stats: {membershipStats.error}
                  </p>
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
                Verify membership status, message authenticity, and role
                permissions for pod security
              </Card.Description>
            </Card.Content>

            {/* Membership Verification */}
            <Card.Content>
              <Header size="small">Verify Membership Status</Header>
              <Form>
                <Form.Group widths="equal">
                  <Form.Input
                    label="Pod ID"
                    onChange={(e) => setVerifyPodId(e.target.value)}
                    placeholder="pod:artist:mb:daft-punk-hash"
                    value={verifyPodId}
                  />
                  <Form.Input
                    label="Peer ID"
                    onChange={(e) => setVerifyPeerId(e.target.value)}
                    placeholder="alice"
                    value={verifyPeerId}
                  />
                </Form.Group>
                <Button
                  disabled={
                    verifyingMembership ||
                    !verifyPodId.trim() ||
                    !verifyPeerId.trim()
                  }
                  fluid
                  loading={verifyingMembership}
                  onClick={handleVerifyPodMembership}
                >
                  Verify Membership
                </Button>
              </Form>

              {membershipVerificationResult && (
                <div style={{ marginTop: '1em' }}>
                  {membershipVerificationResult.error ? (
                    <Message error>
                      <p>
                        Failed to verify membership:{' '}
                        {membershipVerificationResult.error}
                      </p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>
                        Membership Verification Result
                      </Message.Header>
                      <p>
                        <strong>Valid Member:</strong>{' '}
                        {membershipVerificationResult.isValidMember
                          ? 'Yes'
                          : 'No'}
                        <br />
                        <strong>Role:</strong>{' '}
                        {membershipVerificationResult.role || 'None'}
                        <br />
                        <strong>Banned:</strong>{' '}
                        {membershipVerificationResult.isBanned ? 'Yes' : 'No'}
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
                      onChange={(e) => setMessageToVerify(e.target.value)}
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello everyone!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      rows={4}
                      value={messageToVerify}
                    />
                    <Button
                      disabled={verifyingMessage || !messageToVerify.trim()}
                      fluid
                      loading={verifyingMessage}
                      onClick={handleVerifyMessage}
                    >
                      Verify Message
                    </Button>
                  </Form>

                  {messageVerificationResult && (
                    <div style={{ marginTop: '1em' }}>
                      {messageVerificationResult.error ? (
                        <Message error>
                          <p>
                            Failed to verify message:{' '}
                            {messageVerificationResult.error}
                          </p>
                        </Message>
                      ) : (
                        <Message info>
                          <Message.Header>
                            Message Verification Result
                          </Message.Header>
                          <p>
                            <strong>Valid:</strong>{' '}
                            {messageVerificationResult.isValid ? 'Yes' : 'No'}
                            <br />
                            <strong>From Valid Member:</strong>{' '}
                            {messageVerificationResult.isFromValidMember
                              ? 'Yes'
                              : 'No'}
                            <br />
                            <strong>Not Banned:</strong>{' '}
                            {messageVerificationResult.isNotBanned
                              ? 'Yes'
                              : 'No'}
                            <br />
                            <strong>Valid Signature:</strong>{' '}
                            {messageVerificationResult.hasValidSignature
                              ? 'Yes'
                              : 'No'}
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
                        onChange={(e) => setRoleCheckPodId(e.target.value)}
                        placeholder="pod:artist:mb:daft-punk-hash"
                        value={roleCheckPodId}
                      />
                      <Form.Input
                        label="Peer ID"
                        onChange={(e) => setRoleCheckPeerId(e.target.value)}
                        placeholder="alice"
                        value={roleCheckPeerId}
                      />
                    </Form.Group>
                    <Form.Select
                      label="Required Role"
                      onChange={(e, { value }) => setRequiredRole(value)}
                      options={[
                        { key: 'member', text: 'Member', value: 'member' },
                        { key: 'mod', text: 'Moderator', value: 'mod' },
                        { key: 'owner', text: 'Owner', value: 'owner' },
                      ]}
                      value={requiredRole}
                    />
                    <Button
                      disabled={
                        checkingRole ||
                        !roleCheckPodId.trim() ||
                        !roleCheckPeerId.trim()
                      }
                      fluid
                      loading={checkingRole}
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
                            <strong>Has Required Role ({requiredRole}):</strong>{' '}
                            {roleCheckResult.hasRole ? 'Yes' : 'No'}
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
                  disabled={loadingVerificationStats}
                  loading={loadingVerificationStats}
                  onClick={handleLoadVerificationStats}
                  primary
                >
                  Load Verification Stats
                </Button>
              </Button.Group>

              {verificationStats && !verificationStats.error && (
                <div style={{ marginTop: '1em' }}>
                  <Message>
                    <Message.Header>Verification Statistics</Message.Header>
                    <p>
                      <strong>Total Verifications:</strong>{' '}
                      {verificationStats.totalVerifications}
                      <br />
                      <strong>Successful:</strong>{' '}
                      {verificationStats.successfulVerifications}
                      <br />
                      <strong>Failed Membership:</strong>{' '}
                      {verificationStats.failedMembershipChecks}
                      <br />
                      <strong>Failed Signatures:</strong>{' '}
                      {verificationStats.failedSignatureChecks}
                      <br />
                      <strong>Banned Rejections:</strong>{' '}
                      {verificationStats.bannedMemberRejections}
                      <br />
                      <strong>Avg Time:</strong>{' '}
                      {verificationStats.averageVerificationTimeMs.toFixed(2)}ms
                      <br />
                      <strong>Last Verification:</strong>{' '}
                      {verificationStats.lastVerification
                        ? new Date(
                            verificationStats.lastVerification,
                          ).toLocaleString()
                        : 'Never'}
                    </p>
                  </Message>
                </div>
              )}

              {verificationStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
                  <p>
                    Failed to load verification stats: {verificationStats.error}
                  </p>
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
                Discover pods via DHT using name slugs, tags, and content
                associations
              </Card.Description>
            </Card.Content>

            {/* Pod Registration */}
            <Card.Content>
              <Header size="small">Register Pod for Discovery</Header>
              <Form>
                <Form.TextArea
                  label="Pod JSON (must have Visibility: Listed)"
                  onChange={(e) => setPodToRegister(e.target.value)}
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "name": "Daft Punk Fans", "visibility": "Listed", "focusContentId": "content:audio:artist:daft-punk", "tags": ["electronic", "french-house"]}'
                  rows={3}
                  value={podToRegister}
                />
                <Button
                  disabled={registeringPod || !podToRegister.trim()}
                  loading={registeringPod}
                  onClick={handleRegisterPodForDiscovery}
                  primary
                >
                  Register Pod
                </Button>
              </Form>

              {podRegistrationResult && (
                <div style={{ marginTop: '1em' }}>
                  {podRegistrationResult.error ? (
                    <Message error>
                      <p>
                        Failed to register pod: {podRegistrationResult.error}
                      </p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>
                        Pod Registered for Discovery
                      </Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {podRegistrationResult.podId}
                        <br />
                        <strong>Discovery Keys:</strong>{' '}
                        {podRegistrationResult.discoveryKeys?.join(', ')}
                        <br />
                        <strong>Registered:</strong>{' '}
                        {new Date(
                          podRegistrationResult.registeredAt,
                        ).toLocaleString()}
                        <br />
                        <strong>Expires:</strong>{' '}
                        {new Date(
                          podRegistrationResult.expiresAt,
                        ).toLocaleString()}
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
                  onChange={(e) => setPodToUnregister(e.target.value)}
                  placeholder="pod:artist:mb:daft-punk-hash"
                  value={podToUnregister}
                />
                <Button
                  color="red"
                  disabled={unregisteringPod || !podToUnregister.trim()}
                  loading={unregisteringPod}
                  onClick={handleUnregisterPodFromDiscovery}
                >
                  Unregister Pod
                </Button>
              </Form>

              {podUnregistrationResult && (
                <div style={{ marginTop: '1em' }}>
                  {podUnregistrationResult.error ? (
                    <Message error>
                      <p>
                        Failed to unregister pod:{' '}
                        {podUnregistrationResult.error}
                      </p>
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
                      onChange={(e) => setDiscoverByName(e.target.value)}
                      placeholder="daft-punk-fans"
                      value={discoverByName}
                    />
                    <Button
                      disabled={discoveringByName || !discoverByName.trim()}
                      fluid
                      loading={discoveringByName}
                      onClick={handleDiscoverByName}
                    >
                      Discover
                    </Button>
                  </Form>

                  {nameDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {nameDiscoveryResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{nameDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      onChange={(e) => setDiscoverByTag(e.target.value)}
                      placeholder="electronic"
                      value={discoverByTag}
                    />
                    <Button
                      disabled={discoveringByTag || !discoverByTag.trim()}
                      fluid
                      loading={discoveringByTag}
                      onClick={handleDiscoverByTag}
                    >
                      Discover
                    </Button>
                  </Form>

                  {tagDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {tagDiscoveryResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{tagDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      onChange={(e) => setDiscoverTags(e.target.value)}
                      placeholder="electronic,french-house"
                      value={discoverTags}
                    />
                    <Button
                      disabled={discoveringByTags || !discoverTags.trim()}
                      fluid
                      loading={discoveringByTags}
                      onClick={handleDiscoverByTags}
                    >
                      Discover
                    </Button>
                  </Form>

                  {tagsDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {tagsDiscoveryResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{tagsDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      max="1000"
                      min="1"
                      onChange={(e) =>
                        setDiscoverLimit(Number.parseInt(e.target.value) || 50)
                      }
                      type="number"
                      value={discoverLimit}
                    />
                    <Button
                      disabled={discoveringAll}
                      fluid
                      loading={discoveringAll}
                      onClick={handleDiscoverAll}
                    >
                      Discover
                    </Button>
                  </Form>

                  {allDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {allDiscoveryResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{allDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      onChange={(e) => setDiscoverByContent(e.target.value)}
                      placeholder="content:audio:artist:daft-punk"
                      value={discoverByContent}
                    />
                    <Button
                      disabled={
                        discoveringByContent || !discoverByContent.trim()
                      }
                      fluid
                      loading={discoveringByContent}
                      onClick={handleDiscoverByContent}
                    >
                      Discover
                    </Button>
                  </Form>

                  {contentDiscoveryResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {contentDiscoveryResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{contentDiscoveryResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      disabled={loadingDiscoveryStats}
                      loading={loadingDiscoveryStats}
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
                          <strong>Registered Pods:</strong>{' '}
                          {discoveryStats.totalRegisteredPods}
                          <br />
                          <strong>Active Entries:</strong>{' '}
                          {discoveryStats.activeDiscoveryEntries}
                          <br />
                          <strong>Expired Entries:</strong>{' '}
                          {discoveryStats.expiredEntries}
                          <br />
                          <strong>Avg Search Time:</strong>{' '}
                          {discoveryStats.averageDiscoveryTime?.totalMilliseconds.toFixed(
                            0,
                          )}
                          ms
                        </p>
                      </Message>
                    </div>
                  )}

                  {discoveryStats?.error && (
                    <Message
                      error
                      size="tiny"
                      style={{ marginTop: '0.5em' }}
                    >
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
                Manage signed pod membership operations with cryptographic
                verification and role-based approvals
              </Card.Description>
            </Card.Content>

            {/* Join Request */}
            <Card.Content>
              <Header size="small">Request to Join Pod</Header>
              <Form>
                <Form.TextArea
                  label="Join Request JSON (signed by requester)"
                  onChange={(e) => setJoinRequestData(e.target.value)}
                  placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "requestedRole": "member", "publicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Please let me join!"}'
                  rows={4}
                  value={joinRequestData}
                />
                <Button
                  disabled={requestingJoin || !joinRequestData.trim()}
                  loading={requestingJoin}
                  onClick={handleRequestJoin}
                  primary
                >
                  Submit Join Request
                </Button>
              </Form>

              {joinRequestResult && (
                <div style={{ marginTop: '1em' }}>
                  {joinRequestResult.error ? (
                    <Message error>
                      <p>
                        Failed to submit join request: {joinRequestResult.error}
                      </p>
                    </Message>
                  ) : (
                    <Message success>
                      <Message.Header>Join Request Submitted</Message.Header>
                      <p>
                        <strong>Pod ID:</strong> {joinRequestResult.podId}
                        <br />
                        <strong>Peer ID:</strong> {joinRequestResult.peerId}
                        <br />
                        <strong>Status:</strong>{' '}
                        {joinRequestResult.success
                          ? 'Pending approval'
                          : 'Failed'}
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
                      onChange={(e) => setAcceptanceData(e.target.value)}
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "acceptedRole": "member", "acceptorPeerId": "bob", "acceptorPublicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Welcome!"}'
                      rows={4}
                      value={acceptanceData}
                    />
                    <Button
                      disabled={acceptingJoin || !acceptanceData.trim()}
                      loading={acceptingJoin}
                      onClick={handleAcceptJoin}
                      positive
                    >
                      Accept Join
                    </Button>
                  </Form>

                  {acceptanceResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {acceptanceResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{acceptanceResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      onChange={(e) => setLeaveRequestData(e.target.value)}
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "publicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Goodbye!"}'
                      rows={4}
                      value={leaveRequestData}
                    />
                    <Button
                      disabled={requestingLeave || !leaveRequestData.trim()}
                      loading={requestingLeave}
                      onClick={handleRequestLeave}
                    >
                      Submit Leave Request
                    </Button>
                  </Form>

                  {leaveRequestResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {leaveRequestResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{leaveRequestResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                  <Header size="small">
                    Accept Leave Request (Owner/Mod Only)
                  </Header>
                  <Form>
                    <Form.TextArea
                      label="Leave Acceptance JSON (signed by owner/mod)"
                      onChange={(e) => setAcceptanceData(e.target.value)}
                      placeholder='{"podId": "pod:artist:mb:daft-punk-hash", "peerId": "alice", "acceptorPeerId": "bob", "acceptorPublicKey": "base64-ed25519-public-key", "timestampUnixMs": 1703123456789, "signature": "base64-signature", "message": "Farewell!"}'
                      rows={4}
                      value={acceptanceData}
                    />
                    <Button
                      disabled={acceptingLeave || !acceptanceData.trim()}
                      loading={acceptingLeave}
                      negative
                      onClick={handleAcceptLeave}
                    >
                      Accept Leave
                    </Button>
                  </Form>

                  {leaveAcceptanceResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {leaveAcceptanceResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{leaveAcceptanceResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
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
                      onChange={(e) => setPendingPodId(e.target.value)}
                      placeholder="pod:artist:mb:daft-punk-hash"
                      value={pendingPodId}
                    />
                    <Button
                      disabled={loadingPendingRequests || !pendingPodId.trim()}
                      loading={loadingPendingRequests}
                      onClick={handleLoadPendingRequests}
                    >
                      Load Pending Requests
                    </Button>
                  </Form>

                  {pendingJoinRequests && !pendingJoinRequests.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <strong>Join Requests:</strong>{' '}
                        {pendingJoinRequests.pendingJoinRequests?.length || 0}
                      </Message>
                    </div>
                  )}

                  {pendingLeaveRequests && !pendingLeaveRequests.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <strong>Leave Requests:</strong>{' '}
                        {pendingLeaveRequests.pendingLeaveRequests?.length || 0}
                      </Message>
                    </div>
                  )}

                  {(pendingJoinRequests?.error ||
                    pendingLeaveRequests?.error) && (
                    <Message
                      error
                      size="tiny"
                      style={{ marginTop: '0.5em' }}
                    >
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
                Decentralized message routing via overlay network with fanout
                and deduplication for reliable pod communication
              </Card.Description>
            </Card.Content>

            {/* Manual Message Routing */}
            <Card.Content>
              <Header size="small">Manual Message Routing</Header>
              <Form>
                <Form.TextArea
                  label="Pod Message JSON"
                  onChange={(e) => setRouteMessageData(e.target.value)}
                  placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                  rows={4}
                  value={routeMessageData}
                />
                <Button
                  disabled={routingMessage || !routeMessageData.trim()}
                  loading={routingMessage}
                  onClick={handleRouteMessage}
                  primary
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
                      <Message.Header>
                        Message Routed Successfully
                      </Message.Header>
                      <p>
                        <strong>Message ID:</strong> {routingResult.messageId}
                        <br />
                        <strong>Pod ID:</strong> {routingResult.podId}
                        <br />
                        <strong>Target Peers:</strong>{' '}
                        {routingResult.targetPeerCount}
                        <br />
                        <strong>Successfully Routed:</strong>{' '}
                        {routingResult.successfullyRoutedCount}
                        <br />
                        <strong>Failed:</strong>{' '}
                        {routingResult.failedRoutingCount}
                        <br />
                        <strong>Duration:</strong>{' '}
                        {routingResult.routingDuration?.totalMilliseconds?.toFixed(
                          0,
                        )}
                        ms
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
                      onChange={(e) => setRouteToPeersMessage(e.target.value)}
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Direct message", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      rows={3}
                      value={routeToPeersMessage}
                    />
                    <Form.Input
                      label="Target Peer IDs (comma-separated)"
                      onChange={(e) => setRouteToPeersIds(e.target.value)}
                      placeholder="bob,charlie,diana"
                      value={routeToPeersIds}
                    />
                    <Button
                      disabled={
                        routingToPeers ||
                        !routeToPeersMessage.trim() ||
                        !routeToPeersIds.trim()
                      }
                      fluid
                      loading={routingToPeers}
                      onClick={handleRouteMessageToPeers}
                    >
                      Route to Peers
                    </Button>
                  </Form>

                  {routingToPeersResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {routingToPeersResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{routingToPeersResult.error}</p>
                        </Message>
                      ) : (
                        <Message
                          info
                          size="tiny"
                        >
                          <p>
                            Routed to{' '}
                            {routingToPeersResult.successfullyRoutedCount}/
                            {routingToPeersResult.targetPeerCount} peers
                          </p>
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
                        onChange={(e) => setCheckMessageId(e.target.value)}
                        placeholder="msg123"
                        value={checkMessageId}
                      />
                      <Form.Input
                        label="Pod ID"
                        onChange={(e) => setCheckPodId(e.target.value)}
                        placeholder="pod:artist:mb:daft-punk-hash"
                        value={checkPodId}
                      />
                    </Form.Group>
                    <Button.Group fluid>
                      <Button
                        disabled={
                          checkingMessageSeen ||
                          !checkMessageId.trim() ||
                          !checkPodId.trim()
                        }
                        loading={checkingMessageSeen}
                        onClick={handleCheckMessageSeen}
                      >
                        Check Seen
                      </Button>
                      <Button
                        color="blue"
                        disabled={!checkMessageId.trim() || !checkPodId.trim()}
                        onClick={handleRegisterMessageSeen}
                      >
                        Mark Seen
                      </Button>
                    </Button.Group>
                  </Form>

                  {messageSeenResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {messageSeenResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{messageSeenResult.error}</p>
                        </Message>
                      ) : (
                        <Message size="tiny">
                          <p>
                            Message{' '}
                            {messageSeenResult.isSeen
                              ? 'has been'
                              : 'has not been'}{' '}
                            seen in pod {messageSeenResult.podId}
                          </p>
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
                  disabled={loadingRoutingStats}
                  loading={loadingRoutingStats}
                  onClick={handleLoadRoutingStats}
                  primary
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
                      <strong>Total Messages Routed:</strong>{' '}
                      {routingStats.totalMessagesRouted}
                      <br />
                      <strong>Total Routing Attempts:</strong>{' '}
                      {routingStats.totalRoutingAttempts}
                      <br />
                      <strong>Successful Routes:</strong>{' '}
                      {routingStats.successfulRoutingCount}
                      <br />
                      <strong>Failed Routes:</strong>{' '}
                      {routingStats.failedRoutingCount}
                      <br />
                      <strong>Avg Routing Time:</strong>{' '}
                      {routingStats.averageRoutingTimeMs.toFixed(2)}ms
                      <br />
                      <strong>Deduplication Items:</strong>{' '}
                      {routingStats.activeDeduplicationItems}
                      <br />
                      <strong>Bloom Filter Fill:</strong>{' '}
                      {(routingStats.bloomFilterFillRatio * 100).toFixed(1)}%
                      <br />
                      <strong>Est. False Positive:</strong>{' '}
                      {(routingStats.estimatedFalsePositiveRate * 100).toFixed(
                        4,
                      )}
                      %<br />
                      <strong>Last Operation:</strong>{' '}
                      {routingStats.lastRoutingOperation
                        ? new Date(
                            routingStats.lastRoutingOperation,
                          ).toLocaleString()
                        : 'Never'}
                    </p>
                  </Message>

                  <Button
                    color="blue"
                    loading={rebuildIndexLoading}
                    onClick={() => handleRebuildSearchIndex()}
                    size="tiny"
                  >
                    Rebuild Search Index
                  </Button>
                  <Button
                    color="orange"
                    loading={vacuumLoading}
                    onClick={() => handleVacuumDatabase()}
                    size="tiny"
                  >
                    Vacuum Database
                  </Button>
                </div>
              )}

              {routingStats?.error && (
                <Message
                  error
                  style={{ marginTop: '1em' }}
                >
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
                SQLite-backed message storage with full-text search and
                retention policies
              </Card.Description>
            </Card.Content>

            {/* Message Storage */}
            <Card.Content>
              <Header size="small">Storage Management</Header>

              <div style={{ marginBottom: '1em' }}>
                <Button
                  color="teal"
                  loading={storageStatsLoading}
                  onClick={() => handleGetStorageStats()}
                  size="small"
                >
                  Get Storage Stats
                </Button>

                <Button
                  color="purple"
                  loading={cleanupLoading}
                  onClick={() => handleCleanupMessages()}
                  size="small"
                >
                  Cleanup Old Messages (30 days)
                </Button>

                <Button
                  color="blue"
                  loading={rebuildIndexLoading}
                  onClick={() => handleRebuildSearchIndex()}
                  size="small"
                >
                  Rebuild Search Index
                </Button>

                <Button
                  color="orange"
                  loading={vacuumLoading}
                  onClick={() => handleVacuumDatabase()}
                  size="small"
                >
                  Vacuum Database
                </Button>
              </div>

              {storageStats && (
                <Message
                  size="small"
                  style={{ marginBottom: '1em' }}
                >
                  <Message.Header>Message Storage Statistics</Message.Header>
                  <p>
                    <strong>Total Messages:</strong>{' '}
                    {storageStats.totalMessages?.toLocaleString() || 0}
                    <br />
                    <strong>Estimated Size:</strong>{' '}
                    {(storageStats.totalSizeBytes / (1_024 * 1_024)).toFixed(2)}{' '}
                    MB
                    <br />
                    <strong>Oldest Message:</strong>{' '}
                    {storageStats.oldestMessage
                      ? new Date(storageStats.oldestMessage).toLocaleString()
                      : 'None'}
                    <br />
                    <strong>Newest Message:</strong>{' '}
                    {storageStats.newestMessage
                      ? new Date(storageStats.newestMessage).toLocaleString()
                      : 'None'}
                    <br />
                    <strong>Pods with Messages:</strong>{' '}
                    {Object.keys(storageStats.messagesPerPod || {}).length}
                    <br />
                    <strong>Active Channels:</strong>{' '}
                    {Object.keys(storageStats.messagesPerChannel || {}).length}
                  </p>
                </Message>
              )}

              <Header size="small">Message Search</Header>
              <Input
                action={
                  <Button
                    color="green"
                    disabled={!searchQuery.trim()}
                    loading={searchLoading}
                    onClick={() => handleSearchMessages()}
                  >
                    Search
                  </Button>
                }
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search messages..."
                style={{ marginBottom: '1em', width: '100%' }}
                value={searchQuery}
              />

              {searchResults && searchResults.length > 0 && (
                <Message size="small">
                  <Message.Header>
                    Search Results ({searchResults.length})
                  </Message.Header>
                  <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                    {searchResults.map((message, index) => (
                      <div
                        key={index}
                        style={{
                          border: '1px solid #ddd',
                          borderRadius: '4px',
                          marginBottom: '0.5em',
                          padding: '0.5em',
                        }}
                      >
                        <small style={{ color: '#666' }}>
                          {new Date(message.timestampUnixMs).toLocaleString()} •{' '}
                          {message.senderPeerId} • {message.channelId}
                        </small>
                        <div style={{ marginTop: '0.25em' }}>
                          {message.body}
                        </div>
                      </div>
                    ))}
                  </div>
                </Message>
              )}

              {searchResults && searchResults.length === 0 && searchQuery && (
                <Message
                  size="small"
                  warning
                >
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
                  color="purple"
                  loading={backfillStatsLoading}
                  onClick={() => handleGetBackfillStats()}
                  size="small"
                >
                  Get Backfill Stats
                </Button>
              </div>

              {backfillStats && (
                <Message
                  size="small"
                  style={{ marginBottom: '1em' }}
                >
                  <Message.Header>Backfill Statistics</Message.Header>
                  <p>
                    <strong>Requests Sent:</strong>{' '}
                    {backfillStats.totalBackfillRequestsSent?.toLocaleString() ||
                      0}
                    <br />
                    <strong>Requests Received:</strong>{' '}
                    {backfillStats.totalBackfillRequestsReceived?.toLocaleString() ||
                      0}
                    <br />
                    <strong>Messages Backfilled:</strong>{' '}
                    {backfillStats.totalMessagesBackfilled?.toLocaleString() ||
                      0}
                    <br />
                    <strong>Data Transferred:</strong>{' '}
                    {(
                      backfillStats.totalBackfillBytesTransferred /
                      (1_024 * 1_024)
                    ).toFixed(2)}{' '}
                    MB
                    <br />
                    <strong>Avg Duration:</strong>{' '}
                    {backfillStats.averageBackfillDurationMs?.toFixed(2) || 0}ms
                    <br />
                    <strong>Last Operation:</strong>{' '}
                    {backfillStats.lastBackfillOperation
                      ? new Date(
                          backfillStats.lastBackfillOperation,
                        ).toLocaleString()
                      : 'Never'}
                  </p>
                </Message>
              )}

              <Header size="small">Pod Backfill Sync</Header>
              <Input
                action={
                  <>
                    <Button
                      color="blue"
                      disabled={!backfillPodId.trim()}
                      onClick={() => handleGetLastSeenTimestamps()}
                    >
                      Get Timestamps
                    </Button>
                    <Button
                      color="green"
                      disabled={!backfillPodId.trim()}
                      loading={syncBackfillLoading}
                      onClick={() => handleSyncPodBackfill()}
                    >
                      Sync Backfill
                    </Button>
                  </>
                }
                onChange={(e) => setBackfillPodId(e.target.value)}
                placeholder="Pod ID for backfill sync"
                style={{ marginBottom: '1em', width: '100%' }}
                value={backfillPodId}
              />

              {lastSeenTimestamps &&
                Object.keys(lastSeenTimestamps).length > 0 && (
                  <Message size="small">
                    <Message.Header>
                      Last Seen Timestamps for Pod {backfillPodId}
                    </Message.Header>
                    <div style={{ maxHeight: '150px', overflowY: 'auto' }}>
                      {Object.entries(lastSeenTimestamps).map(
                        ([channelId, timestamp]) => (
                          <div
                            key={channelId}
                            style={{ marginBottom: '0.25em' }}
                          >
                            <strong>{channelId}:</strong>{' '}
                            {new Date(timestamp).toLocaleString()}
                          </div>
                        ),
                      )}
                    </div>
                  </Message>
                )}

              {lastSeenTimestamps &&
                Object.keys(lastSeenTimestamps).length === 0 && (
                  <Message
                    info
                    size="small"
                  >
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
                Create, update, and manage channels within pods for organized
                messaging
              </Card.Description>
            </Card.Content>

            {/* Channel Management */}
            <Card.Content>
              <Header size="small">Pod Channel Operations</Header>

              <Input
                action={
                  <Button
                    color="blue"
                    disabled={!channelPodId.trim()}
                    loading={channelsLoading}
                    onClick={() => handleGetChannels()}
                  >
                    Load Channels
                  </Button>
                }
                onChange={(e) => setChannelPodId(e.target.value)}
                placeholder="Pod ID for channel management"
                style={{ marginBottom: '1em', width: '100%' }}
                value={channelPodId}
              />

              {/* Create New Channel */}
              <Header size="tiny">Create New Channel</Header>
              <Input
                action={
                  <>
                    <select
                      onChange={(e) => setNewChannelKind(e.target.value)}
                      style={{
                        border: '1px solid #ccc',
                        borderRadius: '4px',
                        padding: '0.5em',
                      }}
                      value={newChannelKind}
                    >
                      <option value="General">General</option>
                      <option value="Custom">Custom</option>
                      <option value="Bound">Bound</option>
                    </select>
                    <Button
                      color="green"
                      disabled={!newChannelName.trim() || !channelPodId.trim()}
                      loading={createChannelLoading}
                      onClick={() => handleCreateChannel()}
                    >
                      Create
                    </Button>
                  </>
                }
                onChange={(e) => setNewChannelName(e.target.value)}
                placeholder="Channel name"
                style={{ marginBottom: '1em', width: '100%' }}
                value={newChannelName}
              />

              {/* Channels List */}
              {channels.length > 0 && (
                <div>
                  <Header size="tiny">Existing Channels</Header>
                  <div style={{ maxHeight: '400px', overflowY: 'auto' }}>
                    {channels.map((channel) => (
                      <Card
                        key={channel.channelId}
                        style={{ marginBottom: '0.5em' }}
                      >
                        <Card.Content style={{ padding: '0.5em' }}>
                          {editingChannel &&
                          editingChannel.channelId === channel.channelId ? (
                            <div>
                              <Input
                                action={
                                  <>
                                    <Button
                                      color="green"
                                      disabled={!editChannelName.trim()}
                                      loading={updateChannelLoading}
                                      onClick={() =>
                                        handleUpdateChannel(channel.channelId)
                                      }
                                      size="small"
                                    >
                                      Save
                                    </Button>
                                    <Button
                                      onClick={() => cancelEditingChannel()}
                                      size="small"
                                    >
                                      Cancel
                                    </Button>
                                  </>
                                }
                                onChange={(e) =>
                                  setEditChannelName(e.target.value)
                                }
                                placeholder="Channel name"
                                style={{ width: '100%' }}
                                value={editChannelName}
                              />
                            </div>
                          ) : (
                            <div
                              style={{
                                alignItems: 'center',
                                display: 'flex',
                                justifyContent: 'space-between',
                              }}
                            >
                              <div>
                                <strong>{channel.name}</strong>
                                <div
                                  style={{
                                    color: '#666',
                                    fontSize: '0.8em',
                                    marginTop: '0.25em',
                                  }}
                                >
                                  ID: {channel.channelId} • Type: {channel.kind}
                                  {channel.bindingInfo &&
                                    ` • Binding: ${channel.bindingInfo}`}
                                </div>
                              </div>
                              <div>
                                <Button
                                  disabled={
                                    channel.name.toLowerCase() === 'general' &&
                                    channel.kind === 'General'
                                  }
                                  onClick={() => startEditingChannel(channel)}
                                  size="tiny"
                                >
                                  Edit
                                </Button>
                                <Button
                                  color="red"
                                  disabled={
                                    channel.name.toLowerCase() === 'general' &&
                                    channel.kind === 'General'
                                  }
                                  loading={deleteChannelLoading}
                                  onClick={() =>
                                    handleDeleteChannel(
                                      channel.channelId,
                                      channel.name,
                                    )
                                  }
                                  size="tiny"
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
                <Message
                  info
                  size="small"
                >
                  No channels found in pod {channelPodId}. Create the first
                  channel above.
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
                Create pods linked to specific content (music, videos, etc.) for
                focused discussions
              </Card.Description>
            </Card.Content>

            {/* Content Linking */}
            <Card.Content>
              <Header size="small">Content Search & Validation</Header>

              {/* Content Search */}
              <Input
                action={
                  <Button
                    color="blue"
                    disabled={!contentSearchQuery.trim()}
                    loading={contentSearchLoading}
                    onClick={() => handleSearchContent()}
                  >
                    Search
                  </Button>
                }
                onChange={(e) => setContentSearchQuery(e.target.value)}
                placeholder="Search for content (artist, album, movie, etc.)"
                style={{ marginBottom: '1em', width: '100%' }}
                value={contentSearchQuery}
              />

              {/* Search Results */}
              {contentSearchResults.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Search Results</Header>
                  {contentSearchResults.map((item, index) => (
                    <Card
                      key={index}
                      onClick={() => selectContentFromSearch(item)}
                      style={{ cursor: 'pointer', marginBottom: '0.5em' }}
                    >
                      <Card.Content style={{ padding: '0.5em' }}>
                        <strong>{item.title}</strong>
                        {item.subtitle && <div>{item.subtitle}</div>}
                        <small>
                          {item.domain} • {item.type}
                        </small>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}

              {/* Content Validation */}
              <Input
                action={
                  <Button
                    color="green"
                    disabled={!contentId.trim()}
                    loading={contentValidationLoading}
                    onClick={() => handleValidateContentId()}
                  >
                    Validate
                  </Button>
                }
                onChange={(e) => setContentId(e.target.value)}
                placeholder="Content ID (e.g., content:audio:album:mb-release-id)"
                style={{ marginBottom: '1em', width: '100%' }}
                value={contentId}
              />

              {/* Validation Result */}
              {contentValidation && (
                <Message
                  negative={!contentValidation.isValid}
                  positive={contentValidation.isValid}
                  size="small"
                  style={{ marginBottom: '1em' }}
                >
                  <Message.Header>
                    {contentValidation.isValid
                      ? '✓ Valid Content ID'
                      : '✗ Invalid Content ID'}
                  </Message.Header>
                  {!contentValidation.isValid &&
                    contentValidation.errorMessage && (
                      <p>{contentValidation.errorMessage}</p>
                    )}
                </Message>
              )}

              {/* Content Metadata */}
              {contentMetadata && (
                <Message
                  info
                  size="small"
                  style={{ marginBottom: '1em' }}
                >
                  <Message.Header>Content Metadata</Message.Header>
                  <p>
                    <strong>Title:</strong> {contentMetadata.title}
                    <br />
                    <strong>Artist:</strong> {contentMetadata.artist}
                    <br />
                    <strong>Type:</strong> {contentMetadata.type} (
                    {contentMetadata.domain})
                  </p>
                </Message>
              )}

              {/* Pod Creation */}
              {contentValidation?.isValid && (
                <div>
                  <Header size="small">Create Content-Linked Pod</Header>

                  <Input
                    onChange={(e) => setNewPodName(e.target.value)}
                    placeholder="Pod name (auto-filled from content)"
                    style={{ marginBottom: '1em', width: '100%' }}
                    value={newPodName}
                  />

                  <div style={{ marginBottom: '1em' }}>
                    <label style={{ marginRight: '1em' }}>Visibility:</label>
                    <select
                      onChange={(e) => setNewPodVisibility(e.target.value)}
                      value={newPodVisibility}
                    >
                      <option value="Unlisted">Unlisted</option>
                      <option value="Listed">Listed</option>
                      <option value="Private">Private</option>
                    </select>
                  </div>

                  <Button
                    color="teal"
                    disabled={!newPodName.trim()}
                    loading={createPodLoading}
                    onClick={() => handleCreateContentLinkedPod()}
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
                Publish and view opinions on content variants within pods for
                quality assessment and community feedback
              </Card.Description>
            </Card.Content>

            {/* Opinion Management */}
            <Card.Content>
              <Header size="small">Opinion Management</Header>

              {/* Pod Selection */}
              <Input
                onChange={(e) => setOpinionPodId(e.target.value)}
                placeholder="Pod ID"
                style={{ marginBottom: '1em', width: '100%' }}
                value={opinionPodId}
              />

              <Button
                color="blue"
                disabled={!opinionPodId.trim()}
                loading={refreshOpinionsLoading}
                onClick={() => handleRefreshOpinions()}
                style={{ marginBottom: '1em' }}
              >
                Refresh Pod Opinions
              </Button>

              {/* Content Opinions */}
              <Header size="tiny">Content Opinions</Header>
              <Input
                onChange={(e) => setOpinionContentId(e.target.value)}
                placeholder="Content ID (e.g., content:audio:album:mb-id)"
                style={{ marginBottom: '1em', width: '100%' }}
                value={opinionContentId}
              />

              <div style={{ marginBottom: '1em' }}>
                <Button
                  color="teal"
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  loading={getOpinionsLoading}
                  onClick={() => handleGetOpinions()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Opinions
                </Button>

                <Button
                  color="purple"
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  loading={getStatsLoading}
                  onClick={() => handleGetOpinionStatistics()}
                >
                  Get Statistics
                </Button>
              </div>

              {/* Opinion Statistics */}
              {opinionStatistics && (
                <Message
                  info
                  style={{ marginBottom: '1em' }}
                >
                  <Message.Header>Opinion Statistics</Message.Header>
                  <p>
                    <strong>Total Opinions:</strong>{' '}
                    {opinionStatistics.totalOpinions}
                    <br />
                    <strong>Unique Variants:</strong>{' '}
                    {opinionStatistics.uniqueVariants}
                    <br />
                    <strong>Average Score:</strong>{' '}
                    {opinionStatistics.averageScore.toFixed(1)}
                    <br />
                    <strong>Score Range:</strong> {opinionStatistics.minScore} -{' '}
                    {opinionStatistics.maxScore}
                    <br />
                    <strong>Last Updated:</strong>{' '}
                    {new Date(opinionStatistics.lastUpdated).toLocaleString()}
                  </p>
                </Message>
              )}

              {/* Opinions List */}
              {opinions.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Opinions ({opinions.length})</Header>
                  {opinions.map((opinion, index) => (
                    <Card
                      key={index}
                      style={{ marginBottom: '0.5em' }}
                    >
                      <Card.Content style={{ padding: '0.5em' }}>
                        <div
                          style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                          }}
                        >
                          <div>
                            <strong>Variant:</strong>{' '}
                            {opinion.variantHash.slice(0, 8)}...
                            <br />
                            <strong>Score:</strong> {opinion.score}/10
                            {opinion.note && (
                              <>
                                <br />
                                <strong>Note:</strong> {opinion.note}
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
                onChange={(e) => setOpinionVariantHash(e.target.value)}
                placeholder="Variant Hash"
                style={{ marginBottom: '1em', width: '100%' }}
                value={opinionVariantHash}
              />

              <div style={{ marginBottom: '1em' }}>
                <label style={{ marginRight: '1em' }}>Score (0-10):</label>
                <input
                  max="10"
                  min="0"
                  onChange={(e) =>
                    setOpinionScore(Number.parseFloat(e.target.value))
                  }
                  step="0.5"
                  style={{ width: '200px' }}
                  type="range"
                  value={opinionScore}
                />
                <span style={{ marginLeft: '1em' }}>{opinionScore}/10</span>
              </div>

              <Input
                onChange={(e) => setOpinionNote(e.target.value)}
                placeholder="Optional note about this variant"
                style={{ marginBottom: '1em', width: '100%' }}
                value={opinionNote}
              />

              <Button
                color="green"
                disabled={
                  !opinionPodId.trim() ||
                  !opinionContentId.trim() ||
                  !opinionVariantHash.trim()
                }
                loading={publishOpinionLoading}
                onClick={() => handlePublishOpinion()}
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
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  loading={getAggregatedLoading}
                  onClick={() => handleGetAggregatedOpinions()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Aggregated Opinions
                </Button>

                <Button
                  color="blue"
                  disabled={!opinionPodId.trim()}
                  loading={getAffinitiesLoading}
                  onClick={() => handleGetMemberAffinities()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Member Affinities
                </Button>

                <Button
                  color="teal"
                  disabled={!opinionPodId.trim() || !opinionContentId.trim()}
                  loading={getRecommendationsLoading}
                  onClick={() => handleGetConsensusRecommendations()}
                  style={{ marginRight: '0.5em' }}
                >
                  Get Recommendations
                </Button>

                <Button
                  color="orange"
                  disabled={!opinionPodId.trim()}
                  loading={updateAffinitiesLoading}
                  onClick={() => handleUpdateMemberAffinities()}
                >
                  Update Affinities
                </Button>
              </div>

              {/* Aggregated Opinions */}
              {aggregatedOpinions && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Aggregated Opinion Results</Header>
                  <Message info>
                    <strong>Weighted Average:</strong>{' '}
                    {aggregatedOpinions.weightedAverageScore.toFixed(2)}/10
                    <br />
                    <strong>Unweighted Average:</strong>{' '}
                    {aggregatedOpinions.unweightedAverageScore.toFixed(2)}/10
                    <br />
                    <strong>Consensus Strength:</strong>{' '}
                    {(aggregatedOpinions.consensusStrength * 100).toFixed(1)}%
                    <br />
                    <strong>Total Opinions:</strong>{' '}
                    {aggregatedOpinions.totalOpinions}
                    <br />
                    <strong>Unique Variants:</strong>{' '}
                    {aggregatedOpinions.uniqueVariants}
                    <br />
                    <strong>Contributing Members:</strong>{' '}
                    {aggregatedOpinions.contributingMembers}
                  </Message>

                  {/* Variant Breakdown */}
                  {aggregatedOpinions.variantAggregates.length > 0 && (
                    <div style={{ marginTop: '1em' }}>
                      <Header size="tiny">Variant Analysis</Header>
                      {aggregatedOpinions.variantAggregates.map(
                        (variant, index) => (
                          <Card
                            key={index}
                            style={{ marginBottom: '0.5em' }}
                          >
                            <Card.Content style={{ padding: '0.5em' }}>
                              <div>
                                <strong>Variant:</strong>{' '}
                                {variant.variantHash.slice(0, 8)}...
                                <br />
                                <strong>Weighted Score:</strong>{' '}
                                {variant.weightedAverageScore.toFixed(2)}/10
                                <br />
                                <strong>Unweighted Score:</strong>{' '}
                                {variant.unweightedAverageScore.toFixed(2)}/10
                                <br />
                                <strong>Opinions:</strong>{' '}
                                {variant.opinionCount}
                                <br />
                                <strong>Agreement:</strong>{' '}
                                {(
                                  1 -
                                  variant.scoreStandardDeviation / 5
                                ).toFixed(2)}{' '}
                                (lower std dev = higher agreement)
                              </div>
                            </Card.Content>
                          </Card>
                        ),
                      )}
                    </div>
                  )}
                </div>
              )}

              {/* Consensus Recommendations */}
              {consensusRecommendations.length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">Consensus Recommendations</Header>
                  {consensusRecommendations.map((rec, index) => (
                    <Card
                      key={index}
                      style={{
                        borderLeft:
                          rec.recommendation === 'StronglyRecommended'
                            ? '5px solid #21ba45'
                            : rec.recommendation === 'Recommended'
                              ? '5px solid #2185d0'
                              : rec.recommendation === 'Neutral'
                                ? '5px solid #fbbd08'
                                : rec.recommendation === 'NotRecommended'
                                  ? '5px solid #f2711c'
                                  : '5px solid #db2828',
                        marginBottom: '0.5em',
                      }}
                    >
                      <Card.Content style={{ padding: '0.5em' }}>
                        <div>
                          <strong>Variant:</strong>{' '}
                          {rec.variantHash.slice(0, 8)}...
                          <br />
                          <strong>Recommendation:</strong>{' '}
                          {rec.recommendation
                            .replaceAll(/([A-Z])/g, ' $1')
                            .trim()}
                          <br />
                          <strong>Consensus Score:</strong>{' '}
                          {(rec.consensusScore * 100).toFixed(1)}%<br />
                          <strong>Reasoning:</strong> {rec.reasoning}
                          <br />
                          <small>
                            <strong>Factors:</strong>{' '}
                            {rec.supportingFactors.join(', ')}
                          </small>
                        </div>
                      </Card.Content>
                    </Card>
                  ))}
                </div>
              )}

              {/* Member Affinities */}
              {Object.keys(memberAffinities).length > 0 && (
                <div style={{ marginBottom: '1em' }}>
                  <Header size="tiny">
                    Member Affinities ({Object.keys(memberAffinities).length})
                  </Header>
                  {Object.entries(memberAffinities).map(
                    ([peerId, affinity], index) => (
                      <Card
                        key={index}
                        style={{ marginBottom: '0.5em' }}
                      >
                        <Card.Content style={{ padding: '0.5em' }}>
                          <div>
                            <strong>Peer:</strong> {peerId.slice(0, 8)}...
                            <br />
                            <strong>Affinity Score:</strong>{' '}
                            {(affinity.affinityScore * 100).toFixed(1)}%<br />
                            <strong>Trust Score:</strong>{' '}
                            {(affinity.trustScore * 100).toFixed(1)}%<br />
                            <strong>Messages:</strong> {affinity.messageCount}
                            <br />
                            <strong>Opinions:</strong> {affinity.opinionCount}
                            <br />
                            <small>
                              Last Activity:{' '}
                              {new Date(
                                affinity.lastActivity,
                              ).toLocaleDateString()}
                            </small>
                          </div>
                        </Card.Content>
                      </Card>
                    ),
                  )}
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
                Cryptographic signing and verification of pod messages for
                authenticity and integrity
              </Card.Description>
            </Card.Content>

            {/* Message Signing */}
            <Card.Content>
              <Header size="small">Sign Pod Message</Header>
              <Form>
                <Form.TextArea
                  label="Pod Message JSON"
                  onChange={(e) => setMessageToSign(e.target.value)}
                  placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789}'
                  rows={3}
                  value={messageToSign}
                />
                <Form.Input
                  label="Private Key"
                  onChange={(e) => setPrivateKeyForSigning(e.target.value)}
                  placeholder="base64-encoded private key"
                  type="password"
                  value={privateKeyForSigning}
                />
                <Button
                  disabled={
                    signingMessage ||
                    !messageToSign.trim() ||
                    !privateKeyForSigning.trim()
                  }
                  loading={signingMessage}
                  onClick={handleSignMessage}
                  primary
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
                      <Message.Header>
                        Message Signed Successfully
                      </Message.Header>
                      <p>
                        <strong>Message ID:</strong>{' '}
                        {signedMessageResult.messageId}
                        <br />
                        <strong>Channel:</strong>{' '}
                        {signedMessageResult.channelId}
                        <br />
                        <strong>Signature:</strong>{' '}
                        {signedMessageResult.signature?.slice(0, 50)}...
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
                      onChange={(e) => setMessageToVerify(e.target.value)}
                      placeholder='{"messageId": "msg123", "channelId": "pod:artist:mb:daft-punk-hash:general", "senderPeerId": "alice", "body": "Hello pod!", "timestampUnixMs": 1703123456789, "signature": "base64-signature"}'
                      rows={4}
                      value={messageToVerify}
                    />
                    <Button
                      disabled={verifyingSignature || !messageToVerify.trim()}
                      fluid
                      loading={verifyingSignature}
                      onClick={handleVerifySignature}
                    >
                      Verify Signature
                    </Button>
                  </Form>

                  {verificationResult && (
                    <div style={{ marginTop: '0.5em' }}>
                      {verificationResult.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{verificationResult.error}</p>
                        </Message>
                      ) : (
                        <Message size="tiny">
                          <p>
                            Message {verificationResult.messageId}: Signature is{' '}
                            {verificationResult.isValid ? 'VALID' : 'INVALID'}
                          </p>
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
                      disabled={generatingKeyPair}
                      fluid
                      loading={generatingKeyPair}
                      onClick={handleGenerateKeyPair}
                    >
                      Generate New Key Pair
                    </Button>
                  </Form>

                  {generatedKeyPair && (
                    <div style={{ marginTop: '0.5em' }}>
                      {generatedKeyPair.error ? (
                        <Message
                          error
                          size="tiny"
                        >
                          <p>{generatedKeyPair.error}</p>
                        </Message>
                      ) : (
                        <Message
                          size="tiny"
                          success
                        >
                          <Message.Header>Key Pair Generated</Message.Header>
                          <p>
                            <strong>Public Key:</strong>{' '}
                            {generatedKeyPair.publicKey?.slice(0, 30)}...
                            <br />
                            <strong>Private Key:</strong>{' '}
                            {generatedKeyPair.privateKey?.slice(0, 30)}...
                            <br />
                            <em>⚠️ Keep private key secure!</em>
                          </p>
                        </Message>
                      )}
                    </div>
                  )}

                  {/* Signing Statistics */}
                  <Header
                    size="small"
                    style={{ marginTop: '1em' }}
                  >
                    Signing Statistics
                  </Header>
                  <Button.Group fluid>
                    <Button
                      disabled={loadingSigningStats}
                      loading={loadingSigningStats}
                      onClick={handleLoadSigningStats}
                    >
                      Load Stats
                    </Button>
                  </Button.Group>

                  {signingStats && !signingStats.error && (
                    <div style={{ marginTop: '0.5em' }}>
                      <Message size="tiny">
                        <p>
                          <strong>Signatures Created:</strong>{' '}
                          {signingStats.totalSignaturesCreated}
                          <br />
                          <strong>Signatures Verified:</strong>{' '}
                          {signingStats.totalSignaturesVerified}
                          <br />
                          <strong>Successful:</strong>{' '}
                          {signingStats.successfulVerifications}
                          <br />
                          <strong>Failed:</strong>{' '}
                          {signingStats.failedVerifications}
                          <br />
                          <strong>Avg Sign Time:</strong>{' '}
                          {signingStats.averageSigningTimeMs.toFixed(2)}ms
                          <br />
                          <strong>Avg Verify Time:</strong>{' '}
                          {signingStats.averageVerificationTimeMs.toFixed(2)}ms
                        </p>
                      </Message>
                    </div>
                  )}

                  {signingStats?.error && (
                    <Message
                      error
                      size="tiny"
                      style={{ marginTop: '0.5em' }}
                    >
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
              <List
                divided
                relaxed
              >
                {supportedAlgorithms.algorithms.map((alg) => (
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
