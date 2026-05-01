import * as optionsApi from '../../../lib/options';
import * as YAML from 'yaml';
import React, { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Header,
  Icon,
  Label,
  Message,
  Popup,
  Segment,
  Table,
} from 'semantic-ui-react';

const getOption = (source, ...keys) => {
  for (const key of keys) {
    if (source && Object.prototype.hasOwnProperty.call(source, key)) {
      return source[key];
    }
  }

  return undefined;
};

const getAuthOptions = (options = {}) =>
  getOption(getOption(options, 'web', 'Web') || {}, 'authentication', 'Authentication') ||
  {};

const getTransferOptions = (options = {}) =>
  getOption(options, 'transfers', 'Transfers', 'global', 'Global') || {};

const getUploadOptions = (options = {}) =>
  getOption(getTransferOptions(options), 'upload', 'Upload') || {};

const getDownloadOptions = (options = {}) =>
  getOption(getTransferOptions(options), 'download', 'Download') || {};

const getIntegrationOptions = (options = {}) =>
  getOption(options, 'integration', 'Integration', 'integrations', 'Integrations') ||
  {};

const getShareOptions = (options = {}) => getOption(options, 'shares', 'Shares') || {};

const isConfigured = (value) =>
  value !== undefined && value !== null && value !== '';

const toLines = (value = []) => {
  if (Array.isArray(value)) {
    return value.join('\n');
  }

  return String(value || '');
};

const parseLines = (value = '') =>
  String(value)
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

const toNumber = (value, fallback) => {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const boolLabel = (value, trueText = 'Enabled', falseText = 'Disabled') => (
  <Label color={value ? 'green' : 'grey'}>
    <Icon name={value ? 'check circle' : 'minus circle'} />
    {value ? trueText : falseText}
  </Label>
);

const buildForm = (options = {}) => {
  const auth = getAuthOptions(options);
  const jwt = getOption(auth, 'jwt', 'Jwt') || {};
  const passthrough = getOption(auth, 'passthrough', 'Passthrough') || {};
  const apiKeys = getOption(auth, 'apiKeys', 'api_keys', 'ApiKeys') || {};
  const web = getOption(options, 'web', 'Web') || {};
  const https = getOption(web, 'https', 'Https', 'HTTPS') || {};
  const certificate = getOption(https, 'certificate', 'Certificate') || {};
  const rateLimiting = getOption(web, 'rateLimiting', 'rate_limiting', 'RateLimiting') || {};
  const upload = getUploadOptions(options);
  const download = getDownloadOptions(options);
  const autoReplace = getOption(options, 'autoReplace', 'auto_replace', 'AutoReplace') || {};
  const scheduledLimits =
    getOption(options, 'scheduledLimits', 'scheduled_limits', 'ScheduledLimits') || {};
  const filters = getOption(options, 'filters', 'Filters') || {};
  const searchFilters = getOption(filters, 'search', 'Search') || {};
  const blacklist = getOption(options, 'blacklist', 'Blacklist') || {};
  const throttling = getOption(options, 'throttling', 'Throttling') || {};
  const incoming =
    getOption(
      getOption(getOption(throttling, 'search', 'Search') || {}, 'incoming', 'Incoming') ||
        {},
      'incoming',
      'Incoming',
    ) || getOption(getOption(throttling, 'search', 'Search') || {}, 'incoming', 'Incoming') || {};
  const retention = getOption(options, 'retention', 'Retention') || {};
  const retentionTransfers = getOption(retention, 'transfers', 'Transfers') || {};
  const retentionUpload = getOption(retentionTransfers, 'upload', 'Upload') || {};
  const retentionDownload = getOption(retentionTransfers, 'download', 'Download') || {};
  const retentionFiles = getOption(retention, 'files', 'Files') || {};
  const sharesCache = getOption(getShareOptions(options), 'cache', 'Cache') || {};
  const webhooks = getOption(getIntegrationOptions(options), 'webhooks', 'Webhooks') || {};
  const scripts = getOption(getIntegrationOptions(options), 'scripts', 'Scripts') || {};
  const firstWebhookName = Object.keys(webhooks)[0] || 'my_webhook';
  const firstWebhook = webhooks[firstWebhookName] || {};
  const firstWebhookCall = getOption(firstWebhook, 'call', 'Call') || {};
  const firstWebhookRetry = getOption(firstWebhook, 'retry', 'Retry') || {};
  const firstScriptName = Object.keys(scripts)[0] || 'my_script';
  const firstScript = scripts[firstScriptName] || {};
  const firstScriptRun = getOption(firstScript, 'run', 'Run') || {};
  const firstApiKeyName = Object.keys(apiKeys)[0] || 'automation';
  const firstApiKey = apiKeys[firstApiKeyName] || {};
  const retry = getOption(download, 'retry', 'Retry') || {};
  const uploadScheduledLimits =
    getOption(upload, 'scheduledLimits', 'scheduled_limits', 'ScheduledLimits') ||
    scheduledLimits;
  const downloadScheduledLimits =
    getOption(download, 'scheduledLimits', 'scheduled_limits', 'ScheduledLimits') ||
    scheduledLimits;
  const shares = getShareOptions(options);
  const features = getOption(options, 'features', 'Features') || {};
  const dht = getOption(options, 'dht', 'dhtRendezvous', 'DhtRendezvous') || {};
  const rescueMode = getOption(options, 'rescueMode', 'rescue_mode', 'RescueMode') || {};

  return {
    allowRemoteNoAuth: Boolean(getOption(web, 'allowRemoteNoAuth', 'allow_remote_no_auth', 'AllowRemoteNoAuth')),
    apiKeyCidr: getOption(firstApiKey, 'cidr', 'Cidr') || '127.0.0.1/32,::1/128',
    apiKeyConfigured: isConfigured(getOption(firstApiKey, 'key', 'Key')),
    apiKeyName: firstApiKeyName,
    apiKeyRole: getOption(firstApiKey, 'role', 'Role') || 'ReadOnly',
    apiKeyScopes: getOption(firstApiKey, 'scopes', 'Scopes') || '*',
    apiKeyValue: '',
    autoReplaceInterval: String(
      getOption(autoReplace, 'intervalSeconds', 'interval_seconds', 'IntervalSeconds') ??
        getOption(download, 'autoReplaceInterval', 'auto_replace_interval') ??
        300,
    ),
    autoReplaceMaxRetries: String(
      getOption(autoReplace, 'maxRetries', 'max_retries', 'MaxRetries') ?? 3,
    ),
    autoReplaceStuck: Boolean(getOption(download, 'autoReplaceStuck', 'auto_replace_stuck')),
    autoReplaceThreshold: String(
      getOption(
        autoReplace,
        'sizeThresholdPercent',
        'size_threshold_percent',
        'SizeThresholdPercent',
      ) ??
        getOption(download, 'autoReplaceThreshold', 'auto_replace_threshold') ??
        0,
    ),
    blacklistEnabled: Boolean(getOption(blacklist, 'enabled', 'Enabled')),
    blacklistFile: getOption(blacklist, 'file', 'File') || '',
    downloadSlots: String(getOption(download, 'slots', 'Slots') ?? 500),
    downloadSpeedLimit: String(getOption(download, 'speedLimit', 'speed_limit', 'SpeedLimit') ?? 0),
    downloadRetryAttempts: String(getOption(retry, 'attempts', 'Attempts') ?? 1),
    downloadRetryDelay: String(getOption(retry, 'delay', 'Delay') ?? 5000),
    downloadRetryIncomplete: getOption(retry, 'incomplete', 'Incomplete') || 'resume',
    downloadRetryMaxDelay: String(getOption(retry, 'maxDelay', 'max_delay', 'MaxDelay') ?? 60000),
    downloadScheduledLimitsEnabled: Boolean(getOption(downloadScheduledLimits, 'enabled', 'Enabled')),
    dhtAnnounceIntervalSeconds: String(getOption(dht, 'announceIntervalSeconds', 'announce_interval_seconds', 'AnnounceIntervalSeconds') ?? 900),
    dhtBootstrapRouters: toLines(getOption(dht, 'bootstrapRouters', 'bootstrap_routers', 'BootstrapRouters') || []),
    dhtEnabled: getOption(dht, 'enabled', 'Enabled') ?? true,
    dhtLanOnly: Boolean(getOption(dht, 'lanOnly', 'lan_only', 'LanOnly')),
    dhtPort: String(getOption(dht, 'dhtPort', 'dht_port', 'DhtPort') ?? 50305),
    dhtOverlayPort: String(getOption(dht, 'overlayPort', 'overlay_port', 'OverlayPort') ?? 50305),
    enforceSecurity: Boolean(getOption(web, 'enforceSecurity', 'enforce_security', 'EnforceSecurity')),
    eventsRetention: String(getOption(retention, 'events', 'Events') ?? 30),
    featureScenePodBridge: Boolean(getOption(features, 'scenePodBridge', 'ScenePodBridge')),
    fileCompleteRetention: String(getOption(retentionFiles, 'complete', 'Complete') ?? ''),
    fileIncompleteRetention: String(getOption(retentionFiles, 'incomplete', 'Incomplete') ?? ''),
    forceHttps: Boolean(getOption(https, 'force', 'Force')),
    httpsCertificatePassword: '',
    httpsCertificatePasswordConfigured: isConfigured(
      getOption(certificate, 'password', 'Password'),
    ),
    httpsCertificatePfx: getOption(certificate, 'pfx', 'Pfx') || '',
    httpsDisabled: getOption(https, 'disabled', 'Disabled') ?? true,
    httpsPort: String(getOption(https, 'port', 'Port') ?? 5031),
    jwtConfigured: isConfigured(getOption(jwt, 'key', 'Key')),
    jwtKey: '',
    jwtTtl: String(getOption(jwt, 'ttl', 'Ttl') ?? 3600000),
    logRetention: String(getOption(retention, 'logs', 'Logs') ?? 180),
    noAuth: Boolean(getOption(auth, 'disabled', 'Disabled')),
    passthroughCidrs: getOption(passthrough, 'allowedCidrs', 'allowed_cidrs', 'AllowedCidrs') || '',
    rateLimitApi: String(getOption(rateLimiting, 'apiPermitLimit', 'api_permit_limit', 'ApiPermitLimit') ?? 200),
    rateLimitApiWindow: String(getOption(rateLimiting, 'apiWindowSeconds', 'api_window_seconds', 'ApiWindowSeconds') ?? 60),
    rateLimitEnabled: getOption(rateLimiting, 'enabled', 'Enabled') ?? true,
    rateLimitFederation: String(
      getOption(rateLimiting, 'federationPermitLimit', 'federation_permit_limit', 'FederationPermitLimit') ?? 30,
    ),
    rateLimitFederationWindow: String(
      getOption(rateLimiting, 'federationWindowSeconds', 'federation_window_seconds', 'FederationWindowSeconds') ?? 60,
    ),
    rateLimitMesh: String(
      getOption(rateLimiting, 'meshGatewayPermitLimit', 'mesh_gateway_permit_limit', 'MeshGatewayPermitLimit') ?? 60,
    ),
    rateLimitMeshWindow: String(
      getOption(rateLimiting, 'meshGatewayWindowSeconds', 'mesh_gateway_window_seconds', 'MeshGatewayWindowSeconds') ?? 60,
    ),
    retentionDownloadCancelled: String(getOption(retentionDownload, 'cancelled', 'Cancelled') ?? ''),
    retentionDownloadErrored: String(getOption(retentionDownload, 'errored', 'Errored') ?? ''),
    retentionDownloadSucceeded: String(getOption(retentionDownload, 'succeeded', 'Succeeded') ?? ''),
    retentionSearch: String(getOption(retention, 'search', 'Search') ?? ''),
    retentionUploadCancelled: String(getOption(retentionUpload, 'cancelled', 'Cancelled') ?? ''),
    retentionUploadErrored: String(getOption(retentionUpload, 'errored', 'Errored') ?? ''),
    retentionUploadSucceeded: String(getOption(retentionUpload, 'succeeded', 'Succeeded') ?? ''),
    rescueModeEnabled: Boolean(getOption(rescueMode, 'enabled', 'Enabled')),
    rescueModeMaxQueueSeconds: String(getOption(rescueMode, 'maxQueueTimeSeconds', 'max_queue_time_seconds', 'MaxQueueTimeSeconds') ?? 1800),
    scheduledLimitsEnabled: Boolean(getOption(scheduledLimits, 'enabled', 'Enabled')),
    scriptArgs: getOption(firstScriptRun, 'args', 'Args') || '',
    scriptCommand: getOption(firstScriptRun, 'command', 'Command') || '',
    scriptEvents: toLines(getOption(firstScript, 'on', 'On') || []),
    scriptExecutable: getOption(firstScriptRun, 'executable', 'Executable') || '',
    scriptName: firstScriptName,
    searchFilterRequest: toLines(getOption(searchFilters, 'request', 'Request') || []),
    searchIncomingCircuitBreaker: String(
      getOption(incoming, 'circuitBreaker', 'circuit_breaker', 'CircuitBreaker') ?? 500,
    ),
    searchIncomingConcurrency: String(getOption(incoming, 'concurrency', 'Concurrency') ?? 10),
    searchIncomingResponseFileLimit: String(
      getOption(incoming, 'responseFileLimit', 'response_file_limit', 'ResponseFileLimit') ?? 500,
    ),
    shareCacheRetention: String(getOption(sharesCache, 'retention', 'Retention') ?? ''),
    shareCacheWorkers: String(getOption(sharesCache, 'workers', 'Workers') ?? 4),
    shareProbeMediaAttributes: getOption(shares, 'probeMediaAttributes', 'probe_media_attributes', 'ProbeMediaAttributes') ?? true,
    uploadSlots: String(getOption(upload, 'slots', 'Slots') ?? 20),
    uploadSpeedLimit: String(getOption(upload, 'speedLimit', 'speed_limit', 'SpeedLimit') ?? 0),
    uploadScheduledLimitsEnabled: Boolean(getOption(uploadScheduledLimits, 'enabled', 'Enabled')),
    webhookEvents: toLines(getOption(firstWebhook, 'on', 'On') || []),
    webhookIgnoreCertificateErrors: Boolean(getOption(firstWebhookCall, 'ignoreCertificateErrors', 'ignore_certificate_errors', 'IgnoreCertificateErrors')),
    webhookName: firstWebhookName,
    webhookRetryAttempts: String(
      getOption(firstWebhookRetry, 'attempts', 'Attempts') ?? 1,
    ),
    webhookTimeout: String(getOption(firstWebhook, 'timeout', 'Timeout') ?? 5000),
    webhookUrl: getOption(firstWebhookCall, 'url', 'Url') || '',
  };
};

const setOptionalNumber = (document, path, value) => {
  const trimmed = String(value ?? '').trim();
  document.setIn(path, trimmed ? toNumber(trimmed, null) : null);
};

const AdminPolicies = ({ options = {} }) => {
  const remoteConfiguration = Boolean(
    getOption(options, 'remoteConfiguration', 'RemoteConfiguration'),
  );
  const [form, setForm] = useState(() => buildForm(options));
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    setForm(buildForm(options));
  }, [options]);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const missing = [
    form.webhookUrl.trim() &&
      !form.webhookName.trim() &&
      'Webhook settings need a stable name.',
    form.scriptCommand.trim() &&
      !form.scriptName.trim() &&
      'Script settings need a stable name.',
    form.noAuth &&
      !form.passthroughCidrs.trim() &&
      'No-auth mode should keep an explicit loopback CIDR allowlist.',
    !form.httpsDisabled &&
      !form.httpsCertificatePfx.trim() &&
      'HTTPS needs a certificate PFX path.',
    form.blacklistEnabled &&
      !form.blacklistFile.trim() &&
      'Managed blacklist needs a file path.',
  ].filter(Boolean);

  const reset = () => {
    setForm(buildForm(options));
    setMessage(null);
  };

  const saveYaml = async () => {
    setSaving(true);
    setMessage(null);

    try {
      const yaml = await optionsApi.getYaml();
      const document = YAML.parseDocument(yaml || '{}');

      if (form.webhookName.trim()) {
        const base = ['integrations', 'webhooks', form.webhookName.trim()];
        document.setIn([...base, 'on'], parseLines(form.webhookEvents));
        document.setIn([...base, 'call', 'url'], form.webhookUrl.trim());
        document.setIn(
          [...base, 'call', 'ignore_certificate_errors'],
          form.webhookIgnoreCertificateErrors,
        );
        document.setIn([...base, 'timeout'], toNumber(form.webhookTimeout, 5000));
        document.setIn([...base, 'retry', 'attempts'], toNumber(form.webhookRetryAttempts, 1));
      }

      if (form.scriptName.trim()) {
        const base = ['integrations', 'scripts', form.scriptName.trim()];
        document.setIn([...base, 'on'], parseLines(form.scriptEvents));
        document.setIn([...base, 'run', 'command'], form.scriptCommand.trim());
        document.setIn([...base, 'run', 'executable'], form.scriptExecutable.trim());
        document.setIn([...base, 'run', 'args'], form.scriptArgs.trim());
      }

      document.setIn(['transfers', 'upload', 'slots'], toNumber(form.uploadSlots, 20));
      document.setIn(
        ['transfers', 'upload', 'speed_limit'],
        toNumber(form.uploadSpeedLimit, 0),
      );
      document.setIn(['transfers', 'download', 'slots'], toNumber(form.downloadSlots, 500));
      document.setIn(
        ['transfers', 'download', 'speed_limit'],
        toNumber(form.downloadSpeedLimit, 0),
      );
      document.setIn(
        ['transfers', 'download', 'retry', 'incomplete'],
        form.downloadRetryIncomplete,
      );
      document.setIn(
        ['transfers', 'download', 'retry', 'attempts'],
        toNumber(form.downloadRetryAttempts, 1),
      );
      document.setIn(
        ['transfers', 'download', 'retry', 'delay'],
        toNumber(form.downloadRetryDelay, 5000),
      );
      document.setIn(
        ['transfers', 'download', 'retry', 'max_delay'],
        toNumber(form.downloadRetryMaxDelay, 60000),
      );
      document.setIn(['transfers', 'download', 'auto_replace_stuck'], form.autoReplaceStuck);
      document.setIn(
        ['transfers', 'download', 'auto_replace_threshold'],
        Number.parseFloat(form.autoReplaceThreshold) || 0,
      );
      document.setIn(
        ['transfers', 'download', 'auto_replace_interval'],
        toNumber(form.autoReplaceInterval, 60),
      );
      document.setIn(['auto_replace', 'interval_seconds'], toNumber(form.autoReplaceInterval, 300));
      document.setIn(
        ['auto_replace', 'size_threshold_percent'],
        Number.parseFloat(form.autoReplaceThreshold) || 0,
      );
      document.setIn(['auto_replace', 'max_retries'], toNumber(form.autoReplaceMaxRetries, 3));
      document.setIn(['scheduled_limits', 'enabled'], form.scheduledLimitsEnabled);
      document.setIn(
        ['transfers', 'upload', 'scheduled_limits', 'enabled'],
        form.uploadScheduledLimitsEnabled,
      );
      document.setIn(
        ['transfers', 'download', 'scheduled_limits', 'enabled'],
        form.downloadScheduledLimitsEnabled,
      );

      document.setIn(['web', 'enforce_security'], form.enforceSecurity);
      document.setIn(['web', 'allow_remote_no_auth'], form.allowRemoteNoAuth);
      document.setIn(['web', 'authentication', 'disabled'], form.noAuth);
      document.setIn(['web', 'authentication', 'jwt', 'ttl'], toNumber(form.jwtTtl, 3600000));
      if (form.jwtKey.trim()) {
        document.setIn(['web', 'authentication', 'jwt', 'key'], form.jwtKey.trim());
      }
      if (form.apiKeyName.trim()) {
        const base = ['web', 'authentication', 'api_keys', form.apiKeyName.trim()];
        if (form.apiKeyValue.trim()) {
          document.setIn([...base, 'key'], form.apiKeyValue.trim());
        }
        document.setIn([...base, 'role'], form.apiKeyRole);
        document.setIn([...base, 'cidr'], form.apiKeyCidr.trim());
        document.setIn([...base, 'scopes'], form.apiKeyScopes.trim() || '*');
      }
      document.setIn(
        ['web', 'authentication', 'passthrough', 'allowed_cidrs'],
        form.passthroughCidrs.trim(),
      );
      document.setIn(['web', 'https', 'disabled'], form.httpsDisabled);
      document.setIn(['web', 'https', 'port'], toNumber(form.httpsPort, 5031));
      document.setIn(['web', 'https', 'force'], form.forceHttps);
      document.setIn(['web', 'https', 'certificate', 'pfx'], form.httpsCertificatePfx.trim());
      if (form.httpsCertificatePassword.trim()) {
        document.setIn(
          ['web', 'https', 'certificate', 'password'],
          form.httpsCertificatePassword.trim(),
        );
      }
      document.setIn(['web', 'rate_limiting', 'enabled'], form.rateLimitEnabled);
      document.setIn(['web', 'rate_limiting', 'api_permit_limit'], toNumber(form.rateLimitApi, 200));
      document.setIn(
        ['web', 'rate_limiting', 'api_window_seconds'],
        toNumber(form.rateLimitApiWindow, 60),
      );
      document.setIn(
        ['web', 'rate_limiting', 'federation_permit_limit'],
        toNumber(form.rateLimitFederation, 30),
      );
      document.setIn(
        ['web', 'rate_limiting', 'federation_window_seconds'],
        toNumber(form.rateLimitFederationWindow, 60),
      );
      document.setIn(
        ['web', 'rate_limiting', 'mesh_gateway_permit_limit'],
        toNumber(form.rateLimitMesh, 60),
      );
      document.setIn(
        ['web', 'rate_limiting', 'mesh_gateway_window_seconds'],
        toNumber(form.rateLimitMeshWindow, 60),
      );

      document.setIn(['filters', 'search', 'request'], parseLines(form.searchFilterRequest));
      document.setIn(['blacklist', 'enabled'], form.blacklistEnabled);
      document.setIn(['blacklist', 'file'], form.blacklistFile.trim());
      document.setIn(['dht', 'enabled'], form.dhtEnabled);
      document.setIn(['dht', 'lan_only'], form.dhtLanOnly);
      document.setIn(['dht', 'overlay_port'], toNumber(form.dhtOverlayPort, 50305));
      document.setIn(['dht', 'dht_port'], toNumber(form.dhtPort, 50305));
      document.setIn(['dht', 'bootstrap_routers'], parseLines(form.dhtBootstrapRouters));
      document.setIn(
        ['dht', 'announce_interval_seconds'],
        toNumber(form.dhtAnnounceIntervalSeconds, 900),
      );
      document.setIn(['features', 'scene_pod_bridge'], form.featureScenePodBridge);
      document.setIn(['rescue_mode', 'enabled'], form.rescueModeEnabled);
      document.setIn(
        ['rescue_mode', 'max_queue_time_seconds'],
        toNumber(form.rescueModeMaxQueueSeconds, 1800),
      );
      document.setIn(
        ['throttling', 'search', 'incoming', 'concurrency'],
        toNumber(form.searchIncomingConcurrency, 10),
      );
      document.setIn(
        ['throttling', 'search', 'incoming', 'circuit_breaker'],
        toNumber(form.searchIncomingCircuitBreaker, 500),
      );
      document.setIn(
        ['throttling', 'search', 'incoming', 'response_file_limit'],
        toNumber(form.searchIncomingResponseFileLimit, 500),
      );

      setOptionalNumber(document, ['retention', 'search'], form.retentionSearch);
      document.setIn(['retention', 'events'], toNumber(form.eventsRetention, 30));
      document.setIn(['retention', 'logs'], toNumber(form.logRetention, 180));
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'upload', 'succeeded'],
        form.retentionUploadSucceeded,
      );
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'upload', 'errored'],
        form.retentionUploadErrored,
      );
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'upload', 'cancelled'],
        form.retentionUploadCancelled,
      );
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'download', 'succeeded'],
        form.retentionDownloadSucceeded,
      );
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'download', 'errored'],
        form.retentionDownloadErrored,
      );
      setOptionalNumber(
        document,
        ['retention', 'transfers', 'download', 'cancelled'],
        form.retentionDownloadCancelled,
      );
      setOptionalNumber(document, ['retention', 'files', 'complete'], form.fileCompleteRetention);
      setOptionalNumber(document, ['retention', 'files', 'incomplete'], form.fileIncompleteRetention);
      document.setIn(['shares', 'probe_media_attributes'], form.shareProbeMediaAttributes);
      document.setIn(['shares', 'cache', 'workers'], toNumber(form.shareCacheWorkers, 4));
      setOptionalNumber(document, ['shares', 'cache', 'retention'], form.shareCacheRetention);

      await optionsApi.updateYaml({ yaml: document.toString() });
      setForm((current) => ({
        ...current,
        apiKeyConfigured: current.apiKeyConfigured || Boolean(current.apiKeyValue.trim()),
        apiKeyValue: '',
        httpsCertificatePassword: '',
        httpsCertificatePasswordConfigured:
          current.httpsCertificatePasswordConfigured ||
          Boolean(current.httpsCertificatePassword.trim()),
        jwtConfigured: current.jwtConfigured || Boolean(current.jwtKey.trim()),
        jwtKey: '',
      }));
      setMessage({
        positive: true,
        text: 'Policy settings saved to YAML. Restart-signalled options still require a daemon restart.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to save policy settings.',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="admin-policies">
      <Segment>
        <Header as="h3">
          <Icon name="sliders horizontal" />
          Policies
        </Header>
        <p>
          Guided YAML controls for operator policies that are otherwise easy to
          miss in raw configuration.
        </p>
      </Segment>

      {!remoteConfiguration && (
        <Message
          info
          size="small"
        >
          Remote configuration is disabled. These controls show the settings
          shape, but YAML saving is disabled until remote configuration is
          enabled.
        </Message>
      )}

      {message && (
        <Message
          negative={message.negative}
          positive={message.positive}
          size="small"
        >
          {message.text}
        </Message>
      )}
      {missing.length > 0 && (
        <Message
          size="small"
          warning
        >
          <Message.List items={missing} />
        </Message>
      )}

      <Card.Group
        itemsPerRow={1}
        stackable
      >
        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="bolt" />
              Actions: Webhooks and Scripts
            </Card.Header>
            <Card.Meta>Event hooks, remote targets, command hooks, and retry shape.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <Form>
              <Form.Group grouped>
                <Popup
                  content="Probe shared audio files for bitrate, duration, and sample metadata during share scans. Disable on slow or remote storage."
                  trigger={
                    <Checkbox
                      aria-label="Probe share media attributes"
                      checked={form.shareProbeMediaAttributes}
                      disabled={!remoteConfiguration || saving}
                      label="Probe share media attributes"
                      onChange={(_, { checked }) =>
                        update('shareProbeMediaAttributes', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Webhook policy name"
                  disabled={!remoteConfiguration || saving}
                  label="Webhook Name"
                  onChange={(_, { value }) => update('webhookName', value)}
                  value={form.webhookName}
                />
                <Form.Input
                  aria-label="Webhook target URL"
                  disabled={!remoteConfiguration || saving}
                  label="Webhook URL"
                  onChange={(_, { value }) => update('webhookUrl', value)}
                  placeholder="https://example.invalid/slskdn-hook"
                  value={form.webhookUrl}
                />
                <Form.Input
                  aria-label="Webhook retry attempts"
                  disabled={!remoteConfiguration || saving}
                  label="Retry Attempts"
                  min={1}
                  onChange={(_, { value }) => update('webhookRetryAttempts', value)}
                  type="number"
                  value={form.webhookRetryAttempts}
                />
                <Form.Input
                  aria-label="Webhook timeout milliseconds"
                  disabled={!remoteConfiguration || saving}
                  label="Timeout ms"
                  min={1000}
                  onChange={(_, { value }) => update('webhookTimeout', value)}
                  type="number"
                  value={form.webhookTimeout}
                />
              </Form.Group>
              <Form.TextArea
                aria-label="Webhook event names"
                disabled={!remoteConfiguration || saving}
                label="Webhook Events"
                onChange={(_, { value }) => update('webhookEvents', value)}
                placeholder="PrivateMessageReceived&#10;DownloadFileComplete"
                value={form.webhookEvents}
              />
              <Popup
                content="Allow this webhook to call a target with a self-signed or otherwise untrusted certificate."
                trigger={
                  <Checkbox
                    aria-label="Ignore webhook certificate errors"
                    checked={form.webhookIgnoreCertificateErrors}
                    disabled={!remoteConfiguration || saving}
                    label="Ignore webhook certificate errors"
                    onChange={(_, { checked }) =>
                      update('webhookIgnoreCertificateErrors', Boolean(checked))
                    }
                    toggle
                  />
                }
              />
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Script policy name"
                  disabled={!remoteConfiguration || saving}
                  label="Script Name"
                  onChange={(_, { value }) => update('scriptName', value)}
                  value={form.scriptName}
                />
                <Form.Input
                  aria-label="Script command"
                  disabled={!remoteConfiguration || saving}
                  label="Script Command"
                  onChange={(_, { value }) => update('scriptCommand', value)}
                  placeholder="/usr/local/bin/slskdn-hook"
                  value={form.scriptCommand}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Script executable"
                  disabled={!remoteConfiguration || saving}
                  label="Script Executable"
                  onChange={(_, { value }) => update('scriptExecutable', value)}
                  placeholder="/bin/sh"
                  value={form.scriptExecutable}
                />
                <Form.Input
                  aria-label="Script arguments"
                  disabled={!remoteConfiguration || saving}
                  label="Script Args"
                  onChange={(_, { value }) => update('scriptArgs', value)}
                  placeholder="-c ./hook.sh"
                  value={form.scriptArgs}
                />
              </Form.Group>
              <Form.TextArea
                aria-label="Script event names"
                disabled={!remoteConfiguration || saving}
                label="Script Events"
                onChange={(_, { value }) => update('scriptEvents', value)}
                placeholder="DownloadFileComplete"
                value={form.scriptEvents}
              />
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="exchange" />
              Transfer Policy
            </Card.Header>
            <Card.Meta>Slots, speed ceilings, auto-replace, and schedule enablement.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <div className="integration-status-row">
              {boolLabel(form.scheduledLimitsEnabled, 'Schedules On', 'Schedules Off')}
              {boolLabel(form.autoReplaceStuck, 'Auto-Replace On', 'Auto-Replace Off')}
            </div>
            <Form>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Global upload slots"
                  disabled={!remoteConfiguration || saving}
                  label="Upload Slots"
                  onChange={(_, { value }) => update('uploadSlots', value)}
                  type="number"
                  value={form.uploadSlots}
                />
                <Form.Input
                  aria-label="Global upload speed limit"
                  disabled={!remoteConfiguration || saving}
                  label="Upload KiB/s"
                  onChange={(_, { value }) => update('uploadSpeedLimit', value)}
                  type="number"
                  value={form.uploadSpeedLimit}
                />
                <Form.Input
                  aria-label="Global download slots"
                  disabled={!remoteConfiguration || saving}
                  label="Download Slots"
                  onChange={(_, { value }) => update('downloadSlots', value)}
                  type="number"
                  value={form.downloadSlots}
                />
                <Form.Input
                  aria-label="Global download speed limit"
                  disabled={!remoteConfiguration || saving}
                  label="Download KiB/s"
                  onChange={(_, { value }) => update('downloadSpeedLimit', value)}
                  type="number"
                  value={form.downloadSpeedLimit}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Select
                  aria-label="Download retry incomplete strategy"
                  disabled={!remoteConfiguration || saving}
                  label="Retry Partial Files"
                  onChange={(_, { value }) => update('downloadRetryIncomplete', value)}
                  options={[
                    { key: 'resume', text: 'resume', value: 'resume' },
                    { key: 'overwrite', text: 'overwrite', value: 'overwrite' },
                  ]}
                  value={form.downloadRetryIncomplete}
                />
                <Form.Input
                  aria-label="Download retry attempts"
                  disabled={!remoteConfiguration || saving}
                  label="Retry Attempts"
                  min={1}
                  onChange={(_, { value }) => update('downloadRetryAttempts', value)}
                  type="number"
                  value={form.downloadRetryAttempts}
                />
                <Form.Input
                  aria-label="Download retry delay milliseconds"
                  disabled={!remoteConfiguration || saving}
                  label="Retry Delay ms"
                  min={0}
                  onChange={(_, { value }) => update('downloadRetryDelay', value)}
                  type="number"
                  value={form.downloadRetryDelay}
                />
                <Form.Input
                  aria-label="Download retry max delay milliseconds"
                  disabled={!remoteConfiguration || saving}
                  label="Retry Max Delay ms"
                  min={1000}
                  onChange={(_, { value }) => update('downloadRetryMaxDelay', value)}
                  type="number"
                  value={form.downloadRetryMaxDelay}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Auto replace interval seconds"
                  disabled={!remoteConfiguration || saving}
                  label="Auto-Replace Interval"
                  min={60}
                  onChange={(_, { value }) => update('autoReplaceInterval', value)}
                  type="number"
                  value={form.autoReplaceInterval}
                />
                <Form.Input
                  aria-label="Auto replace size threshold"
                  disabled={!remoteConfiguration || saving}
                  label="Size Threshold %"
                  min={0}
                  onChange={(_, { value }) => update('autoReplaceThreshold', value)}
                  type="number"
                  value={form.autoReplaceThreshold}
                />
                <Form.Input
                  aria-label="Auto replace max retries"
                  disabled={!remoteConfiguration || saving}
                  label="Max Retries"
                  min={0}
                  onChange={(_, { value }) => update('autoReplaceMaxRetries', value)}
                  type="number"
                  value={form.autoReplaceMaxRetries}
                />
              </Form.Group>
              <Form.Group grouped>
                <Popup
                  content="Enable automatic replacement of stuck downloads with alternative sources."
                  trigger={
                    <Checkbox
                      aria-label="Enable auto replace stuck downloads"
                      checked={form.autoReplaceStuck}
                      disabled={!remoteConfiguration || saving}
                      label="Enable auto-replace stuck downloads"
                      onChange={(_, { checked }) =>
                        update('autoReplaceStuck', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Enable the legacy top-level scheduled speed-limit policy in YAML."
                  trigger={
                    <Checkbox
                      aria-label="Enable scheduled speed limits"
                      checked={form.scheduledLimitsEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable scheduled speed limits"
                      onChange={(_, { checked }) =>
                        update('scheduledLimitsEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Enable scheduled upload speed limits under global upload policy."
                  trigger={
                    <Checkbox
                      aria-label="Enable upload scheduled speed limits"
                      checked={form.uploadScheduledLimitsEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable upload schedules"
                      onChange={(_, { checked }) =>
                        update('uploadScheduledLimitsEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Enable scheduled download speed limits under global download policy."
                  trigger={
                    <Checkbox
                      aria-label="Enable download scheduled speed limits"
                      checked={form.downloadScheduledLimitsEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable download schedules"
                      onChange={(_, { checked }) =>
                        update('downloadScheduledLimitsEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
              </Form.Group>
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="lock" />
              Security and Access
            </Card.Header>
            <Card.Meta>Authentication, API keys, HTTPS, CORS-adjacent rate limits, and restart cues.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <div className="integration-status-row">
              {boolLabel(!form.noAuth, 'Auth On', 'No Auth')}
              {boolLabel(form.jwtConfigured, 'JWT Key Set', 'JWT Key Missing')}
              {boolLabel(form.apiKeyConfigured, 'API Key Set', 'API Key Missing')}
              {boolLabel(!form.httpsDisabled, 'HTTPS On', 'HTTPS Off')}
              {boolLabel(
                form.httpsCertificatePasswordConfigured,
                'Certificate Password Set',
                'Certificate Password Missing',
              )}
            </div>
            <Form>
              <Form.Group grouped>
                <Popup
                  content="Enable strict startup and request hardening checks for exposed deployments."
                  trigger={
                    <Checkbox
                      aria-label="Enforce web security hardening"
                      checked={form.enforceSecurity}
                      disabled={!remoteConfiguration || saving}
                      label="Enforce security hardening"
                      onChange={(_, { checked }) =>
                        update('enforceSecurity', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Disable Web UI authentication only for tightly controlled loopback deployments."
                  trigger={
                    <Checkbox
                      aria-label="Disable web authentication"
                      checked={form.noAuth}
                      disabled={!remoteConfiguration || saving}
                      label="Disable authentication"
                      onChange={(_, { checked }) => update('noAuth', Boolean(checked))}
                      toggle
                    />
                  }
                />
                <Popup
                  content="Allow no-auth access from explicitly configured non-loopback CIDRs."
                  trigger={
                    <Checkbox
                      aria-label="Allow remote no-auth CIDRs"
                      checked={form.allowRemoteNoAuth}
                      disabled={!remoteConfiguration || saving}
                      label="Allow remote no-auth CIDRs"
                      onChange={(_, { checked }) =>
                        update('allowRemoteNoAuth', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Enable HTTP rate limiting for API, federation, and mesh gateway policies."
                  trigger={
                    <Checkbox
                      aria-label="Enable HTTP rate limiting"
                      checked={form.rateLimitEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable HTTP rate limiting"
                      onChange={(_, { checked }) =>
                        update('rateLimitEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="JWT replacement key"
                  disabled={!remoteConfiguration || saving}
                  label="JWT Key"
                  onChange={(_, { value }) => update('jwtKey', value)}
                  placeholder={form.jwtConfigured ? 'Configured' : 'New JWT key'}
                  type="password"
                  value={form.jwtKey}
                />
                <Form.Input
                  aria-label="JWT TTL milliseconds"
                  disabled={!remoteConfiguration || saving}
                  label="JWT TTL ms"
                  min={3600}
                  onChange={(_, { value }) => update('jwtTtl', value)}
                  type="number"
                  value={form.jwtTtl}
                />
                <Form.Input
                  aria-label="No auth allowed CIDRs"
                  disabled={!remoteConfiguration || saving}
                  label="No-Auth CIDRs"
                  onChange={(_, { value }) => update('passthroughCidrs', value)}
                  value={form.passthroughCidrs}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="API key policy name"
                  disabled={!remoteConfiguration || saving}
                  label="API Key Name"
                  onChange={(_, { value }) => update('apiKeyName', value)}
                  value={form.apiKeyName}
                />
                <Form.Input
                  aria-label="API key replacement value"
                  disabled={!remoteConfiguration || saving}
                  label="API Key"
                  onChange={(_, { value }) => update('apiKeyValue', value)}
                  placeholder={form.apiKeyConfigured ? 'Configured' : 'New API key'}
                  type="password"
                  value={form.apiKeyValue}
                />
                <Form.Select
                  aria-label="API key role"
                  disabled={!remoteConfiguration || saving}
                  label="Role"
                  onChange={(_, { value }) => update('apiKeyRole', value)}
                  options={[
                    { key: 'ReadOnly', text: 'ReadOnly', value: 'ReadOnly' },
                    { key: 'ReadWrite', text: 'ReadWrite', value: 'ReadWrite' },
                    {
                      key: 'Administrator',
                      text: 'Administrator',
                      value: 'Administrator',
                    },
                  ]}
                  value={form.apiKeyRole}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="API key CIDR allowlist"
                  disabled={!remoteConfiguration || saving}
                  label="API CIDRs"
                  onChange={(_, { value }) => update('apiKeyCidr', value)}
                  value={form.apiKeyCidr}
                />
                <Form.Input
                  aria-label="API key scopes"
                  disabled={!remoteConfiguration || saving}
                  label="API Scopes"
                  onChange={(_, { value }) => update('apiKeyScopes', value)}
                  value={form.apiKeyScopes}
                />
              </Form.Group>
              <Form.Group grouped>
                <Popup
                  content="Disable HTTPS listener. Keep this off when exposing the Web UI beyond localhost."
                  trigger={
                    <Checkbox
                      aria-label="Disable HTTPS listener"
                      checked={form.httpsDisabled}
                      disabled={!remoteConfiguration || saving}
                      label="Disable HTTPS"
                      onChange={(_, { checked }) =>
                        update('httpsDisabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Redirect HTTP requests to HTTPS after certificate settings are valid."
                  trigger={
                    <Checkbox
                      aria-label="Force HTTPS redirects"
                      checked={form.forceHttps}
                      disabled={!remoteConfiguration || saving}
                      label="Force HTTPS"
                      onChange={(_, { checked }) =>
                        update('forceHttps', Boolean(checked))
                      }
                    />
                  }
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="HTTPS port"
                  disabled={!remoteConfiguration || saving}
                  label="HTTPS Port"
                  onChange={(_, { value }) => update('httpsPort', value)}
                  type="number"
                  value={form.httpsPort}
                />
                <Form.Input
                  aria-label="HTTPS certificate PFX path"
                  disabled={!remoteConfiguration || saving}
                  label="Certificate PFX"
                  onChange={(_, { value }) => update('httpsCertificatePfx', value)}
                  value={form.httpsCertificatePfx}
                />
                <Form.Input
                  aria-label="HTTPS certificate password"
                  disabled={!remoteConfiguration || saving}
                  label="Certificate Password"
                  onChange={(_, { value }) =>
                    update('httpsCertificatePassword', value)
                  }
                  placeholder={
                    form.httpsCertificatePasswordConfigured
                      ? 'Configured'
                      : 'Certificate password'
                  }
                  type="password"
                  value={form.httpsCertificatePassword}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="API rate limit permits"
                  disabled={!remoteConfiguration || saving}
                  label="API Permits/min"
                  onChange={(_, { value }) => update('rateLimitApi', value)}
                  type="number"
                  value={form.rateLimitApi}
                />
                <Form.Input
                  aria-label="API rate limit window seconds"
                  disabled={!remoteConfiguration || saving}
                  label="API Window sec"
                  onChange={(_, { value }) => update('rateLimitApiWindow', value)}
                  type="number"
                  value={form.rateLimitApiWindow}
                />
                <Form.Input
                  aria-label="Federation rate limit permits"
                  disabled={!remoteConfiguration || saving}
                  label="Federation Permits/min"
                  onChange={(_, { value }) => update('rateLimitFederation', value)}
                  type="number"
                  value={form.rateLimitFederation}
                />
                <Form.Input
                  aria-label="Federation rate limit window seconds"
                  disabled={!remoteConfiguration || saving}
                  label="Federation Window sec"
                  onChange={(_, { value }) =>
                    update('rateLimitFederationWindow', value)
                  }
                  type="number"
                  value={form.rateLimitFederationWindow}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Mesh gateway rate limit permits"
                  disabled={!remoteConfiguration || saving}
                  label="Mesh Permits/min"
                  onChange={(_, { value }) => update('rateLimitMesh', value)}
                  type="number"
                  value={form.rateLimitMesh}
                />
                <Form.Input
                  aria-label="Mesh gateway rate limit window seconds"
                  disabled={!remoteConfiguration || saving}
                  label="Mesh Window sec"
                  onChange={(_, { value }) => update('rateLimitMeshWindow', value)}
                  type="number"
                  value={form.rateLimitMeshWindow}
                />
              </Form.Group>
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="sitemap" />
              Search and Network Policy
            </Card.Header>
            <Card.Meta>Incoming search throttles, request filters, and managed blacklist setup.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <div className="integration-status-row">
              {boolLabel(form.blacklistEnabled, 'Blacklist On', 'Blacklist Off')}
              {boolLabel(form.dhtEnabled, 'DHT On', 'DHT Off')}
              {boolLabel(form.dhtLanOnly, 'LAN Only', 'Public DHT')}
              {boolLabel(form.featureScenePodBridge, 'Bridge On', 'Bridge Off')}
              {boolLabel(form.rescueModeEnabled, 'Rescue On', 'Rescue Off')}
            </div>
            <Form>
              <Form.TextArea
                aria-label="Incoming search request filters"
                disabled={!remoteConfiguration || saving}
                label="Incoming Search Filter Regexes"
                onChange={(_, { value }) => update('searchFilterRequest', value)}
                placeholder="(?i)forbidden term"
                value={form.searchFilterRequest}
              />
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Incoming search concurrency"
                  disabled={!remoteConfiguration || saving}
                  label="Incoming Concurrency"
                  onChange={(_, { value }) =>
                    update('searchIncomingConcurrency', value)
                  }
                  type="number"
                  value={form.searchIncomingConcurrency}
                />
                <Form.Input
                  aria-label="Incoming search circuit breaker"
                  disabled={!remoteConfiguration || saving}
                  label="Circuit Breaker"
                  onChange={(_, { value }) =>
                    update('searchIncomingCircuitBreaker', value)
                  }
                  type="number"
                  value={form.searchIncomingCircuitBreaker}
                />
                <Form.Input
                  aria-label="Incoming search response file limit"
                  disabled={!remoteConfiguration || saving}
                  label="Response File Limit"
                  onChange={(_, { value }) =>
                    update('searchIncomingResponseFileLimit', value)
                  }
                  type="number"
                  value={form.searchIncomingResponseFileLimit}
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Popup
                  content="Enable loading a managed CIDR/P2P/DAT blacklist file at startup."
                  trigger={
                    <Checkbox
                      aria-label="Enable managed blacklist"
                      checked={form.blacklistEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable managed blacklist"
                      onChange={(_, { checked }) =>
                        update('blacklistEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Form.Input
                  aria-label="Managed blacklist file path"
                  disabled={!remoteConfiguration || saving}
                  label="Blacklist File"
                  onChange={(_, { value }) => update('blacklistFile', value)}
                  value={form.blacklistFile}
                />
              </Form.Group>
              <Form.Group grouped>
                <Popup
                  content="Enable DHT rendezvous. Disable this when you want no DHT peer discovery."
                  trigger={
                    <Checkbox
                      aria-label="Enable DHT rendezvous"
                      checked={form.dhtEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable DHT rendezvous"
                      onChange={(_, { checked }) =>
                        update('dhtEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Keep DHT discovery away from public bootstrap routers and use only private or local discovery paths."
                  trigger={
                    <Checkbox
                      aria-label="Use LAN-only DHT rendezvous"
                      checked={form.dhtLanOnly}
                      disabled={!remoteConfiguration || saving}
                      label="LAN-only DHT"
                      onChange={(_, { checked }) =>
                        update('dhtLanOnly', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Opt in to experimental Scene and Pod bridge aggregation."
                  trigger={
                    <Checkbox
                      aria-label="Enable Scene Pod Bridge"
                      checked={form.featureScenePodBridge}
                      disabled={!remoteConfiguration || saving}
                      label="Enable Scene Pod Bridge"
                      onChange={(_, { checked }) =>
                        update('featureScenePodBridge', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
                <Popup
                  content="Enable underperformance rescue policies for queued or stalled downloads."
                  trigger={
                    <Checkbox
                      aria-label="Enable rescue mode"
                      checked={form.rescueModeEnabled}
                      disabled={!remoteConfiguration || saving}
                      label="Enable rescue mode"
                      onChange={(_, { checked }) =>
                        update('rescueModeEnabled', Boolean(checked))
                      }
                      toggle
                    />
                  }
                />
              </Form.Group>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="DHT overlay TCP port"
                  disabled={!remoteConfiguration || saving}
                  label="Overlay TCP Port"
                  onChange={(_, { value }) => update('dhtOverlayPort', value)}
                  type="number"
                  value={form.dhtOverlayPort}
                />
                <Form.Input
                  aria-label="DHT UDP port"
                  disabled={!remoteConfiguration || saving}
                  label="DHT UDP Port"
                  onChange={(_, { value }) => update('dhtPort', value)}
                  type="number"
                  value={form.dhtPort}
                />
                <Form.Input
                  aria-label="DHT announce interval seconds"
                  disabled={!remoteConfiguration || saving}
                  label="DHT Announce sec"
                  onChange={(_, { value }) =>
                    update('dhtAnnounceIntervalSeconds', value)
                  }
                  type="number"
                  value={form.dhtAnnounceIntervalSeconds}
                />
                <Form.Input
                  aria-label="Rescue max queue seconds"
                  disabled={!remoteConfiguration || saving}
                  label="Rescue Queue sec"
                  onChange={(_, { value }) =>
                    update('rescueModeMaxQueueSeconds', value)
                  }
                  type="number"
                  value={form.rescueModeMaxQueueSeconds}
                />
              </Form.Group>
              <Form.TextArea
                aria-label="DHT bootstrap routers"
                disabled={!remoteConfiguration || saving}
                label="DHT Bootstrap Routers"
                onChange={(_, { value }) => update('dhtBootstrapRouters', value)}
                placeholder="router.bittorrent.com"
                value={form.dhtBootstrapRouters}
              />
            </Form>
          </Card.Content>
        </Card>

        <Card fluid>
          <Card.Content>
            <Card.Header>
              <Icon name="archive" />
              Retention and Storage
            </Card.Header>
            <Card.Meta>Search/event/log cleanup, transfer history, file records, and share cache pressure.</Card.Meta>
          </Card.Content>
          <Card.Content>
            <Form>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Search retention minutes"
                  disabled={!remoteConfiguration || saving}
                  label="Search Retention min"
                  onChange={(_, { value }) => update('retentionSearch', value)}
                  value={form.retentionSearch}
                />
                <Form.Input
                  aria-label="Event retention days"
                  disabled={!remoteConfiguration || saving}
                  label="Events Retention days"
                  onChange={(_, { value }) => update('eventsRetention', value)}
                  type="number"
                  value={form.eventsRetention}
                />
                <Form.Input
                  aria-label="Log retention days"
                  disabled={!remoteConfiguration || saving}
                  label="Logs Retention days"
                  onChange={(_, { value }) => update('logRetention', value)}
                  type="number"
                  value={form.logRetention}
                />
              </Form.Group>
              <Table
                celled
                compact
              >
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell>History</Table.HeaderCell>
                    <Table.HeaderCell>Succeeded</Table.HeaderCell>
                    <Table.HeaderCell>Errored</Table.HeaderCell>
                    <Table.HeaderCell>Cancelled</Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  <Table.Row>
                    <Table.Cell>Uploads</Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Upload succeeded retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionUploadSucceeded', value)
                        }
                        value={form.retentionUploadSucceeded}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Upload errored retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionUploadErrored', value)
                        }
                        value={form.retentionUploadErrored}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Upload cancelled retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionUploadCancelled', value)
                        }
                        value={form.retentionUploadCancelled}
                      />
                    </Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.Cell>Downloads</Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Download succeeded retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionDownloadSucceeded', value)
                        }
                        value={form.retentionDownloadSucceeded}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Download errored retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionDownloadErrored', value)
                        }
                        value={form.retentionDownloadErrored}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <Form.Input
                        aria-label="Download cancelled retention minutes"
                        disabled={!remoteConfiguration || saving}
                        onChange={(_, { value }) =>
                          update('retentionDownloadCancelled', value)
                        }
                        value={form.retentionDownloadCancelled}
                      />
                    </Table.Cell>
                  </Table.Row>
                </Table.Body>
              </Table>
              <Form.Group widths="equal">
                <Form.Input
                  aria-label="Complete file retention minutes"
                  disabled={!remoteConfiguration || saving}
                  label="Complete Files min"
                  onChange={(_, { value }) => update('fileCompleteRetention', value)}
                  value={form.fileCompleteRetention}
                />
                <Form.Input
                  aria-label="Incomplete file retention minutes"
                  disabled={!remoteConfiguration || saving}
                  label="Incomplete Files min"
                  onChange={(_, { value }) =>
                    update('fileIncompleteRetention', value)
                  }
                  value={form.fileIncompleteRetention}
                />
                <Form.Input
                  aria-label="Share cache workers"
                  disabled={!remoteConfiguration || saving}
                  label="Share Cache Workers"
                  onChange={(_, { value }) => update('shareCacheWorkers', value)}
                  type="number"
                  value={form.shareCacheWorkers}
                />
                <Form.Input
                  aria-label="Share cache retention minutes"
                  disabled={!remoteConfiguration || saving}
                  label="Share Cache Retention min"
                  onChange={(_, { value }) => update('shareCacheRetention', value)}
                  value={form.shareCacheRetention}
                />
              </Form.Group>
            </Form>
          </Card.Content>
        </Card>
      </Card.Group>

      <div className="integration-actions admin-policy-actions">
        <Popup
          content="Persist the policy settings above to YAML. This does not test webhooks, run scripts, contact peers, restart the daemon, or mutate downloads."
          trigger={
            <Button
              disabled={!remoteConfiguration || missing.length > 0}
              icon
              labelPosition="left"
              loading={saving}
              onClick={saveYaml}
              primary
            >
              <Icon name="file alternate" />
              Save YAML
            </Button>
          }
        />
        <Popup
          content="Discard unsaved policy edits and restore values currently reported by the daemon."
          trigger={
            <Button
              disabled={saving}
              icon
              labelPosition="left"
              onClick={reset}
            >
              <Icon name="undo" />
              Reset
            </Button>
          }
        />
      </div>
    </div>
  );
};

export default AdminPolicies;
