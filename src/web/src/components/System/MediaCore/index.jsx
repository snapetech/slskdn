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
