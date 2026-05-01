import * as lidarr from '../../../lib/lidarr';
import * as optionsApi from '../../../lib/options';
import * as YAML from 'yaml';
import {
  buildMediaServerPathDiagnostic,
  mediaServerAdapters,
} from '../../../lib/mediaServerIntegrations';
import {
  buildServarrReadiness,
  summarizeServarrReadiness,
} from '../../../lib/servarrReadiness';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Header,
  Icon,
  Input,
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

const getIntegrationsOptions = (options = {}) =>
  getOption(options, 'integration', 'Integration', 'integrations', 'Integrations') ||
  {};

const getVpnOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'vpn', 'Vpn', 'VPN') || {};

const getLidarrOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'lidarr', 'Lidarr') || {};

const getSpotifyOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'spotify', 'Spotify') || {};

const getYouTubeOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'youtube', 'YouTube') || {};

const getLastFmOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'lastfm', 'lastFm', 'LastFm') || {};

const getPushbulletOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'pushbullet', 'Pushbullet') || {};

const getNtfyOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'ntfy', 'Ntfy') || {};

const getPushoverOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'pushover', 'Pushover') || {};

const getFtpOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'ftp', 'Ftp', 'FTP') || {};

const getVpnState = (state = {}) => getOption(state, 'vpn', 'Vpn', 'VPN') || {};

const boolLabel = (value, trueText = 'Enabled', falseText = 'Disabled') => (
  <Label color={value ? 'green' : 'grey'}>
    <Icon name={value ? 'check circle' : 'minus circle'} />
    {value ? trueText : falseText}
  </Label>
);

const valueOrDash = (value) =>
  value === undefined || value === null || value === '' ? '-' : value;

const isConfigured = (value) =>
  value !== undefined && value !== null && value !== '';

const toNumber = (value, fallback) => {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const portForwards = (vpn = {}) =>
  getOption(vpn, 'portForwards', 'PortForwards') || [];

const buildSourceFeedForm = (options = {}) => {
  const spotify = getSpotifyOptions(options);
  const youtube = getYouTubeOptions(options);
  const lastfm = getLastFmOptions(options);

  return {
    lastFmApiKey: '',
    lastFmConfigured: isConfigured(getOption(lastfm, 'apiKey', 'ApiKey')),
    lastFmEnabled: Boolean(getOption(lastfm, 'enabled', 'Enabled')),
    spotifyClientId: '',
    spotifyClientSecret: '',
    spotifyConfigured: isConfigured(getOption(spotify, 'clientId', 'ClientId')),
    spotifyEnabled: Boolean(getOption(spotify, 'enabled', 'Enabled')),
    spotifyMaxItems: String(
      getOption(spotify, 'maxItemsPerImport', 'MaxItemsPerImport') ?? 500,
    ),
    spotifyMarket: getOption(spotify, 'market', 'Market') || 'US',
    spotifyRedirectUri: getOption(spotify, 'redirectUri', 'RedirectUri') || '',
    spotifySecretConfigured: isConfigured(
      getOption(spotify, 'clientSecret', 'ClientSecret'),
    ),
    spotifyTimeout: String(getOption(spotify, 'timeoutSeconds', 'TimeoutSeconds') ?? 20),
    youTubeApiKey: '',
    youTubeConfigured: isConfigured(getOption(youtube, 'apiKey', 'ApiKey')),
    youTubeEnabled: Boolean(getOption(youtube, 'enabled', 'Enabled')),
  };
};

const buildNotificationForm = (options = {}) => {
  const pushbullet = getPushbulletOptions(options);
  const ntfy = getNtfyOptions(options);
  const pushover = getPushoverOptions(options);

  return {
    ntfyAccessToken: '',
    ntfyAccessTokenConfigured: isConfigured(
      getOption(ntfy, 'accessToken', 'AccessToken'),
    ),
    ntfyEnabled: Boolean(getOption(ntfy, 'enabled', 'Enabled')),
    ntfyNotifyOnPrivateMessage:
      getOption(ntfy, 'notifyOnPrivateMessage', 'NotifyOnPrivateMessage') ?? true,
    ntfyNotifyOnRoomMention:
      getOption(ntfy, 'notifyOnRoomMention', 'NotifyOnRoomMention') ?? true,
    ntfyPrefix: getOption(ntfy, 'notificationPrefix', 'NotificationPrefix') || 'slskdN',
    ntfyUrl: getOption(ntfy, 'url', 'Url') || '',
    pushbulletAccessToken: '',
    pushbulletAccessTokenConfigured: isConfigured(
      getOption(pushbullet, 'accessToken', 'AccessToken'),
    ),
    pushbulletCooldownTime: String(
      getOption(pushbullet, 'cooldownTime', 'CooldownTime') ?? 900000,
    ),
    pushbulletEnabled: Boolean(getOption(pushbullet, 'enabled', 'Enabled')),
    pushbulletNotifyOnPrivateMessage:
      getOption(
        pushbullet,
        'notifyOnPrivateMessage',
        'NotifyOnPrivateMessage',
      ) ?? true,
    pushbulletNotifyOnRoomMention:
      getOption(pushbullet, 'notifyOnRoomMention', 'NotifyOnRoomMention') ?? true,
    pushbulletPrefix:
      getOption(pushbullet, 'notificationPrefix', 'NotificationPrefix') ||
      'From slskdN:',
    pushbulletRetryAttempts: String(
      getOption(pushbullet, 'retryAttempts', 'RetryAttempts') ?? 3,
    ),
    pushoverEnabled: Boolean(getOption(pushover, 'enabled', 'Enabled')),
    pushoverNotifyOnPrivateMessage:
      getOption(
        pushover,
        'notifyOnPrivateMessage',
        'NotifyOnPrivateMessage',
      ) ?? true,
    pushoverNotifyOnRoomMention:
      getOption(pushover, 'notifyOnRoomMention', 'NotifyOnRoomMention') ?? true,
    pushoverPrefix:
      getOption(pushover, 'notificationPrefix', 'NotificationPrefix') || 'slskdN',
    pushoverToken: '',
    pushoverTokenConfigured: isConfigured(getOption(pushover, 'token', 'Token')),
    pushoverUserKey: '',
    pushoverUserKeyConfigured: isConfigured(
      getOption(pushover, 'userKey', 'UserKey'),
    ),
  };
};

const NotificationIntegrationsPanel = ({ options }) => {
  const remoteConfiguration = Boolean(
    getOption(options, 'remoteConfiguration', 'RemoteConfiguration'),
  );
  const [form, setForm] = useState(() => buildNotificationForm(options));
  const [savingAction, setSavingAction] = useState('');
  const [message, setMessage] = useState(null);
  const saving = Boolean(savingAction);

  useEffect(() => {
    setForm(buildNotificationForm(options));
  }, [options]);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const reset = () => {
    setForm(buildNotificationForm(options));
    setMessage(null);
  };

  const missingRequiredSettings = [
    form.pushbulletEnabled &&
      !form.pushbulletAccessTokenConfigured &&
      !form.pushbulletAccessToken.trim() &&
      'Pushbullet needs an access token before notifications can be sent.',
    form.ntfyEnabled &&
      !form.ntfyUrl.trim() &&
      'Ntfy needs a topic URL before notifications can be sent.',
    form.pushoverEnabled &&
      !form.pushoverUserKeyConfigured &&
      !form.pushoverUserKey.trim() &&
      'Pushover needs a user key before notifications can be sent.',
    form.pushoverEnabled &&
      !form.pushoverTokenConfigured &&
      !form.pushoverToken.trim() &&
      'Pushover needs an API token before notifications can be sent.',
  ].filter(Boolean);

  const buildOverlay = () => {
    const pushbulletPatch = {
      cooldownTime: toNumber(form.pushbulletCooldownTime, 900000),
      enabled: form.pushbulletEnabled,
      notificationPrefix: form.pushbulletPrefix.trim(),
      notifyOnPrivateMessage: form.pushbulletNotifyOnPrivateMessage,
      notifyOnRoomMention: form.pushbulletNotifyOnRoomMention,
      retryAttempts: toNumber(form.pushbulletRetryAttempts, 3),
    };

    if (form.pushbulletAccessToken.trim()) {
      pushbulletPatch.accessToken = form.pushbulletAccessToken.trim();
    }

    const ntfyPatch = {
      enabled: form.ntfyEnabled,
      notificationPrefix: form.ntfyPrefix.trim(),
      notifyOnPrivateMessage: form.ntfyNotifyOnPrivateMessage,
      notifyOnRoomMention: form.ntfyNotifyOnRoomMention,
      url: form.ntfyUrl.trim(),
    };

    if (form.ntfyAccessToken.trim()) {
      ntfyPatch.accessToken = form.ntfyAccessToken.trim();
    }

    const pushoverPatch = {
      enabled: form.pushoverEnabled,
      notificationPrefix: form.pushoverPrefix.trim(),
      notifyOnPrivateMessage: form.pushoverNotifyOnPrivateMessage,
      notifyOnRoomMention: form.pushoverNotifyOnRoomMention,
    };

    if (form.pushoverUserKey.trim()) {
      pushoverPatch.userKey = form.pushoverUserKey.trim();
    }

    if (form.pushoverToken.trim()) {
      pushoverPatch.token = form.pushoverToken.trim();
    }

    return {
      integration: {
        ntfy: ntfyPatch,
        pushbullet: pushbulletPatch,
        pushover: pushoverPatch,
      },
    };
  };

  const markSecretsConfigured = (overlay) => {
    const pushbulletPatch = overlay.integration.pushbullet;
    const ntfyPatch = overlay.integration.ntfy;
    const pushoverPatch = overlay.integration.pushover;

    setForm((current) => ({
      ...current,
      ntfyAccessToken: '',
      ntfyAccessTokenConfigured:
        current.ntfyAccessTokenConfigured || Boolean(ntfyPatch.accessToken),
      pushbulletAccessToken: '',
      pushbulletAccessTokenConfigured:
        current.pushbulletAccessTokenConfigured ||
        Boolean(pushbulletPatch.accessToken),
      pushoverToken: '',
      pushoverTokenConfigured:
        current.pushoverTokenConfigured || Boolean(pushoverPatch.token),
      pushoverUserKey: '',
      pushoverUserKeyConfigured:
        current.pushoverUserKeyConfigured || Boolean(pushoverPatch.userKey),
    }));
  };

  const applyRuntime = async () => {
    setSavingAction('runtime');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      await optionsApi.applyOverlay(overlay);
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'Notification integration settings applied for this running daemon.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to apply notification integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  const saveYaml = async () => {
    setSavingAction('yaml');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      const yaml = await optionsApi.getYaml();
      const document = YAML.parseDocument(yaml || '{}');
      const set = (path, value) => document.setIn(path, value);
      const pushbulletPatch = overlay.integration.pushbullet;
      const ntfyPatch = overlay.integration.ntfy;
      const pushoverPatch = overlay.integration.pushover;

      set(['integrations', 'pushbullet', 'enabled'], pushbulletPatch.enabled);
      set(
        ['integrations', 'pushbullet', 'notification_prefix'],
        pushbulletPatch.notificationPrefix,
      );
      set(
        ['integrations', 'pushbullet', 'notify_on_private_message'],
        pushbulletPatch.notifyOnPrivateMessage,
      );
      set(
        ['integrations', 'pushbullet', 'notify_on_room_mention'],
        pushbulletPatch.notifyOnRoomMention,
      );
      set(
        ['integrations', 'pushbullet', 'retry_attempts'],
        pushbulletPatch.retryAttempts,
      );
      set(
        ['integrations', 'pushbullet', 'cooldown_time'],
        pushbulletPatch.cooldownTime,
      );
      if (pushbulletPatch.accessToken) {
        set(
          ['integrations', 'pushbullet', 'access_token'],
          pushbulletPatch.accessToken,
        );
      }

      set(['integrations', 'ntfy', 'enabled'], ntfyPatch.enabled);
      set(['integrations', 'ntfy', 'url'], ntfyPatch.url);
      set(
        ['integrations', 'ntfy', 'notification_prefix'],
        ntfyPatch.notificationPrefix,
      );
      set(
        ['integrations', 'ntfy', 'notify_on_private_message'],
        ntfyPatch.notifyOnPrivateMessage,
      );
      set(
        ['integrations', 'ntfy', 'notify_on_room_mention'],
        ntfyPatch.notifyOnRoomMention,
      );
      if (ntfyPatch.accessToken) {
        set(['integrations', 'ntfy', 'access_token'], ntfyPatch.accessToken);
      }

      set(['integrations', 'pushover', 'enabled'], pushoverPatch.enabled);
      set(
        ['integrations', 'pushover', 'notification_prefix'],
        pushoverPatch.notificationPrefix,
      );
      set(
        ['integrations', 'pushover', 'notify_on_private_message'],
        pushoverPatch.notifyOnPrivateMessage,
      );
      set(
        ['integrations', 'pushover', 'notify_on_room_mention'],
        pushoverPatch.notifyOnRoomMention,
      );
      if (pushoverPatch.userKey) {
        set(['integrations', 'pushover', 'user_key'], pushoverPatch.userKey);
      }

      if (pushoverPatch.token) {
        set(['integrations', 'pushover', 'token'], pushoverPatch.token);
      }

      await optionsApi.updateYaml({ yaml: document.toString() });
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'Notification integration settings saved to YAML.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to save notification integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="bell" />
          Notifications
        </Card.Header>
        <Card.Meta>
          Push notifications for private messages and room mentions.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(form.pushbulletEnabled, 'Pushbullet On', 'Pushbullet Off')}
          <Label>
            <Icon
              name={form.pushbulletAccessTokenConfigured ? 'key' : 'warning sign'}
            />
            Pushbullet Token{' '}
            {form.pushbulletAccessTokenConfigured ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.ntfyEnabled, 'Ntfy On', 'Ntfy Off')}
          <Label>
            <Icon name={form.ntfyUrl ? 'linkify' : 'warning sign'} />
            Ntfy URL {form.ntfyUrl ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.pushoverEnabled, 'Pushover On', 'Pushover Off')}
          <Label>
            <Icon
              name={
                form.pushoverUserKeyConfigured && form.pushoverTokenConfigured
                  ? 'key'
                  : 'warning sign'
              }
            />
            Pushover Secrets{' '}
            {form.pushoverUserKeyConfigured && form.pushoverTokenConfigured
              ? 'Configured'
              : 'Missing'}
          </Label>
        </div>

        {!remoteConfiguration && (
          <Message
            info
            size="small"
          >
            Runtime configuration changes are disabled. Enable remote
            configuration or edit YAML in the Options tab to change notification
            settings.
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
        {missingRequiredSettings.length > 0 && (
          <Message
            size="small"
            warning
          >
            <Message.List items={missingRequiredSettings} />
          </Message>
        )}

        <Form className="notification-settings-form">
          <Segment>
            <Header as="h4">
              <Icon name="paper plane" />
              Pushbullet
            </Header>
            <Popup
              content="Turn on Pushbullet delivery for enabled notification triggers."
              trigger={
                <Checkbox
                  aria-label="Enable Pushbullet notifications"
                  checked={form.pushbulletEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Pushbullet"
                  onChange={(_, { checked }) =>
                    update('pushbulletEnabled', checked)
                  }
                  toggle
                />
              }
            />
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Pushbullet access token"
                disabled={!remoteConfiguration || saving}
                label="Access Token"
                onChange={(_, { value }) => update('pushbulletAccessToken', value)}
                placeholder={
                  form.pushbulletAccessTokenConfigured
                    ? 'Configured'
                    : 'Pushbullet access token'
                }
                type="password"
                value={form.pushbulletAccessToken}
              />
              <Form.Input
                aria-label="Pushbullet notification prefix"
                disabled={!remoteConfiguration || saving}
                label="Title Prefix"
                onChange={(_, { value }) => update('pushbulletPrefix', value)}
                value={form.pushbulletPrefix}
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Pushbullet retry attempts"
                disabled={!remoteConfiguration || saving}
                label="Retry Attempts"
                max={5}
                min={0}
                onChange={(_, { value }) =>
                  update('pushbulletRetryAttempts', value)
                }
                type="number"
                value={form.pushbulletRetryAttempts}
              />
              <Form.Input
                aria-label="Pushbullet cooldown milliseconds"
                disabled={!remoteConfiguration || saving}
                label="Cooldown Milliseconds"
                min={0}
                onChange={(_, { value }) =>
                  update('pushbulletCooldownTime', value)
                }
                type="number"
                value={form.pushbulletCooldownTime}
              />
            </Form.Group>
            <Form.Group grouped>
              <Popup
                content="Send Pushbullet notifications when a private message arrives."
                trigger={
                  <Checkbox
                    aria-label="Pushbullet private message notifications"
                    checked={form.pushbulletNotifyOnPrivateMessage}
                    disabled={!remoteConfiguration || saving}
                    label="Private messages"
                    onChange={(_, { checked }) =>
                      update('pushbulletNotifyOnPrivateMessage', checked)
                    }
                  />
                }
              />
              <Popup
                content="Send Pushbullet notifications when your username is mentioned in a joined room."
                trigger={
                  <Checkbox
                    aria-label="Pushbullet room mention notifications"
                    checked={form.pushbulletNotifyOnRoomMention}
                    disabled={!remoteConfiguration || saving}
                    label="Room mentions"
                    onChange={(_, { checked }) =>
                      update('pushbulletNotifyOnRoomMention', checked)
                    }
                  />
                }
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="rss" />
              Ntfy
            </Header>
            <Popup
              content="Turn on Ntfy delivery for enabled notification triggers."
              trigger={
                <Checkbox
                  aria-label="Enable Ntfy notifications"
                  checked={form.ntfyEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Ntfy"
                  onChange={(_, { checked }) => update('ntfyEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Ntfy topic URL"
                disabled={!remoteConfiguration || saving}
                label="Topic URL"
                onChange={(_, { value }) => update('ntfyUrl', value)}
                placeholder="https://ntfy.sh/mytopic"
                value={form.ntfyUrl}
              />
              <Form.Input
                aria-label="Ntfy access token"
                disabled={!remoteConfiguration || saving}
                label="Access Token"
                onChange={(_, { value }) => update('ntfyAccessToken', value)}
                placeholder={
                  form.ntfyAccessTokenConfigured
                    ? 'Configured'
                    : 'Optional Ntfy token'
                }
                type="password"
                value={form.ntfyAccessToken}
              />
            </Form.Group>
            <Form.Input
              aria-label="Ntfy notification prefix"
              disabled={!remoteConfiguration || saving}
              label="Title Prefix"
              onChange={(_, { value }) => update('ntfyPrefix', value)}
              value={form.ntfyPrefix}
            />
            <Form.Group grouped>
              <Popup
                content="Send Ntfy notifications when a private message arrives."
                trigger={
                  <Checkbox
                    aria-label="Ntfy private message notifications"
                    checked={form.ntfyNotifyOnPrivateMessage}
                    disabled={!remoteConfiguration || saving}
                    label="Private messages"
                    onChange={(_, { checked }) =>
                      update('ntfyNotifyOnPrivateMessage', checked)
                    }
                  />
                }
              />
              <Popup
                content="Send Ntfy notifications when your username is mentioned in a joined room."
                trigger={
                  <Checkbox
                    aria-label="Ntfy room mention notifications"
                    checked={form.ntfyNotifyOnRoomMention}
                    disabled={!remoteConfiguration || saving}
                    label="Room mentions"
                    onChange={(_, { checked }) =>
                      update('ntfyNotifyOnRoomMention', checked)
                    }
                  />
                }
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="mobile alternate" />
              Pushover
            </Header>
            <Popup
              content="Turn on Pushover delivery for enabled notification triggers."
              trigger={
                <Checkbox
                  aria-label="Enable Pushover notifications"
                  checked={form.pushoverEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Pushover"
                  onChange={(_, { checked }) => update('pushoverEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Pushover user key"
                disabled={!remoteConfiguration || saving}
                label="User Key"
                onChange={(_, { value }) => update('pushoverUserKey', value)}
                placeholder={
                  form.pushoverUserKeyConfigured
                    ? 'Configured'
                    : 'Pushover user key'
                }
                type="password"
                value={form.pushoverUserKey}
              />
              <Form.Input
                aria-label="Pushover API token"
                disabled={!remoteConfiguration || saving}
                label="API Token"
                onChange={(_, { value }) => update('pushoverToken', value)}
                placeholder={
                  form.pushoverTokenConfigured
                    ? 'Configured'
                    : 'Pushover application token'
                }
                type="password"
                value={form.pushoverToken}
              />
            </Form.Group>
            <Form.Input
              aria-label="Pushover notification prefix"
              disabled={!remoteConfiguration || saving}
              label="Title Prefix"
              onChange={(_, { value }) => update('pushoverPrefix', value)}
              value={form.pushoverPrefix}
            />
            <Form.Group grouped>
              <Popup
                content="Send Pushover notifications when a private message arrives."
                trigger={
                  <Checkbox
                    aria-label="Pushover private message notifications"
                    checked={form.pushoverNotifyOnPrivateMessage}
                    disabled={!remoteConfiguration || saving}
                    label="Private messages"
                    onChange={(_, { checked }) =>
                      update('pushoverNotifyOnPrivateMessage', checked)
                    }
                  />
                }
              />
              <Popup
                content="Send Pushover notifications when your username is mentioned in a joined room."
                trigger={
                  <Checkbox
                    aria-label="Pushover room mention notifications"
                    checked={form.pushoverNotifyOnRoomMention}
                    disabled={!remoteConfiguration || saving}
                    label="Room mentions"
                    onChange={(_, { checked }) =>
                      update('pushoverNotifyOnRoomMention', checked)
                    }
                  />
                }
              />
            </Form.Group>
          </Segment>
        </Form>

        <div className="integration-actions">
          <Popup
            content="Apply these notification settings through the runtime configuration overlay."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'runtime'}
                onClick={applyRuntime}
                primary
              >
                <Icon name="save" />
                Apply Runtime
              </Button>
            }
          />
          <Popup
            content="Persist these notification settings to the YAML configuration file."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'yaml'}
                onClick={saveYaml}
              >
                <Icon name="file alternate" />
                Save YAML
              </Button>
            }
          />
          <Popup
            content="Discard unsaved notification edits and restore the values currently reported by the daemon."
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
      </Card.Content>
    </Card>
  );
};

const ftpEncryptionOptions = ['auto', 'none', 'implicit', 'explicit'].map((value) => ({
  key: value,
  text: value,
  value,
}));

const buildFtpForm = (options = {}) => {
  const ftp = getFtpOptions(options);

  return {
    address: getOption(ftp, 'address', 'Address') || '',
    connectionTimeout: String(
      getOption(ftp, 'connectionTimeout', 'ConnectionTimeout') ?? 5000,
    ),
    enabled: Boolean(getOption(ftp, 'enabled', 'Enabled')),
    encryptionMode:
      getOption(ftp, 'encryptionMode', 'EncryptionMode') || 'auto',
    ignoreCertificateErrors: Boolean(
      getOption(ftp, 'ignoreCertificateErrors', 'IgnoreCertificateErrors'),
    ),
    overwriteExisting:
      getOption(ftp, 'overwriteExisting', 'OverwriteExisting') ?? true,
    password: '',
    passwordConfigured: isConfigured(getOption(ftp, 'password', 'Password')),
    port: String(getOption(ftp, 'port', 'Port') ?? 21),
    remotePath: getOption(ftp, 'remotePath', 'RemotePath') || '/',
    retryAttempts: String(getOption(ftp, 'retryAttempts', 'RetryAttempts') ?? 3),
    username: getOption(ftp, 'username', 'Username') || '',
  };
};

const FtpIntegrationPanel = ({ options }) => {
  const remoteConfiguration = Boolean(
    getOption(options, 'remoteConfiguration', 'RemoteConfiguration'),
  );
  const [form, setForm] = useState(() => buildFtpForm(options));
  const [savingAction, setSavingAction] = useState('');
  const [message, setMessage] = useState(null);
  const saving = Boolean(savingAction);

  useEffect(() => {
    setForm(buildFtpForm(options));
  }, [options]);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const reset = () => {
    setForm(buildFtpForm(options));
    setMessage(null);
  };

  const missingRequiredSettings = [
    form.enabled &&
      !form.address.trim() &&
      'FTP needs a server address before completed downloads can be uploaded.',
  ].filter(Boolean);

  const buildOverlay = () => {
    const ftpPatch = {
      address: form.address.trim(),
      connectionTimeout: toNumber(form.connectionTimeout, 5000),
      enabled: form.enabled,
      encryptionMode: form.encryptionMode,
      ignoreCertificateErrors: form.ignoreCertificateErrors,
      overwriteExisting: form.overwriteExisting,
      port: toNumber(form.port, 21),
      remotePath: form.remotePath.trim() || '/',
      retryAttempts: toNumber(form.retryAttempts, 3),
      username: form.username.trim(),
    };

    if (form.password.trim()) {
      ftpPatch.password = form.password.trim();
    }

    return {
      integration: {
        ftp: ftpPatch,
      },
    };
  };

  const markSecretsConfigured = (overlay) => {
    const ftpPatch = overlay.integration.ftp;

    setForm((current) => ({
      ...current,
      password: '',
      passwordConfigured: current.passwordConfigured || Boolean(ftpPatch.password),
    }));
  };

  const applyRuntime = async () => {
    setSavingAction('runtime');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      await optionsApi.applyOverlay(overlay);
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'FTP integration settings applied for this running daemon.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to apply FTP integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  const saveYaml = async () => {
    setSavingAction('yaml');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      const yaml = await optionsApi.getYaml();
      const document = YAML.parseDocument(yaml || '{}');
      const set = (path, value) => document.setIn(path, value);
      const ftpPatch = overlay.integration.ftp;

      set(['integrations', 'ftp', 'enabled'], ftpPatch.enabled);
      set(['integrations', 'ftp', 'address'], ftpPatch.address);
      set(['integrations', 'ftp', 'port'], ftpPatch.port);
      set(['integrations', 'ftp', 'username'], ftpPatch.username);
      if (ftpPatch.password) {
        set(['integrations', 'ftp', 'password'], ftpPatch.password);
      }

      set(['integrations', 'ftp', 'remote_path'], ftpPatch.remotePath);
      set(['integrations', 'ftp', 'encryption_mode'], ftpPatch.encryptionMode);
      set(
        ['integrations', 'ftp', 'ignore_certificate_errors'],
        ftpPatch.ignoreCertificateErrors,
      );
      set(
        ['integrations', 'ftp', 'overwrite_existing'],
        ftpPatch.overwriteExisting,
      );
      set(
        ['integrations', 'ftp', 'connection_timeout'],
        ftpPatch.connectionTimeout,
      );
      set(['integrations', 'ftp', 'retry_attempts'], ftpPatch.retryAttempts);

      await optionsApi.updateYaml({ yaml: document.toString() });
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'FTP integration settings saved to YAML.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to save FTP integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="upload" />
          FTP Uploads
        </Card.Header>
        <Card.Meta>
          Completed-download upload target and connection policy.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(form.enabled, 'FTP On', 'FTP Off')}
          <Label>
            <Icon name={form.address ? 'server' : 'warning sign'} />
            Address {form.address ? 'Configured' : 'Missing'}
          </Label>
          <Label>
            <Icon name={form.passwordConfigured ? 'key' : 'lock'} />
            Password {form.passwordConfigured ? 'Configured' : 'Optional'}
          </Label>
          {boolLabel(
            form.overwriteExisting,
            'Overwrite Existing',
            'Skip Existing',
          )}
        </div>

        {!remoteConfiguration && (
          <Message
            info
            size="small"
          >
            Runtime configuration changes are disabled. Enable remote
            configuration or edit YAML in the Options tab to change FTP
            settings.
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
        {missingRequiredSettings.length > 0 && (
          <Message
            size="small"
            warning
          >
            <Message.List items={missingRequiredSettings} />
          </Message>
        )}

        <Form className="ftp-settings-form">
          <Popup
            content="Turn on FTP uploads after completed downloads. This affects future completed-transfer handling."
            trigger={
              <Checkbox
                aria-label="Enable FTP completed-download uploads"
                checked={form.enabled}
                disabled={!remoteConfiguration || saving}
                label="Enable FTP uploads"
                onChange={(_, { checked }) => update('enabled', checked)}
                toggle
              />
            }
          />
          <Form.Group widths="equal">
            <Form.Input
              aria-label="FTP server address"
              disabled={!remoteConfiguration || saving}
              label="Server Address"
              onChange={(_, { value }) => update('address', value)}
              placeholder="ftp.example.net"
              value={form.address}
            />
            <Form.Input
              aria-label="FTP server port"
              disabled={!remoteConfiguration || saving}
              label="Port"
              max={65535}
              min={1}
              onChange={(_, { value }) => update('port', value)}
              type="number"
              value={form.port}
            />
            <Form.Select
              aria-label="FTP encryption mode"
              disabled={!remoteConfiguration || saving}
              label="Encryption"
              onChange={(_, { value }) => update('encryptionMode', value)}
              options={ftpEncryptionOptions}
              value={form.encryptionMode}
            />
          </Form.Group>
          <Form.Group widths="equal">
            <Form.Input
              aria-label="FTP username"
              disabled={!remoteConfiguration || saving}
              label="Username"
              onChange={(_, { value }) => update('username', value)}
              value={form.username}
            />
            <Form.Input
              aria-label="FTP password"
              disabled={!remoteConfiguration || saving}
              label="Password"
              onChange={(_, { value }) => update('password', value)}
              placeholder={form.passwordConfigured ? 'Configured' : 'Optional password'}
              type="password"
              value={form.password}
            />
          </Form.Group>
          <Form.Input
            aria-label="FTP remote upload path"
            disabled={!remoteConfiguration || saving}
            label="Remote Path"
            onChange={(_, { value }) => update('remotePath', value)}
            value={form.remotePath}
          />
          <Form.Group widths="equal">
            <Form.Input
              aria-label="FTP connection timeout milliseconds"
              disabled={!remoteConfiguration || saving}
              label="Connection Timeout Milliseconds"
              min={0}
              onChange={(_, { value }) => update('connectionTimeout', value)}
              type="number"
              value={form.connectionTimeout}
            />
            <Form.Input
              aria-label="FTP retry attempts"
              disabled={!remoteConfiguration || saving}
              label="Retry Attempts"
              max={5}
              min={0}
              onChange={(_, { value }) => update('retryAttempts', value)}
              type="number"
              value={form.retryAttempts}
            />
          </Form.Group>
          <Form.Group grouped>
            <Popup
              content="Overwrite files already present at the remote FTP path instead of skipping them."
              trigger={
                <Checkbox
                  aria-label="FTP overwrite existing remote files"
                  checked={form.overwriteExisting}
                  disabled={!remoteConfiguration || saving}
                  label="Overwrite existing remote files"
                  onChange={(_, { checked }) =>
                    update('overwriteExisting', checked)
                  }
                />
              }
            />
            <Popup
              content="Allow FTP certificate validation failures for self-signed or untrusted certificates."
              trigger={
                <Checkbox
                  aria-label="FTP ignore certificate errors"
                  checked={form.ignoreCertificateErrors}
                  disabled={!remoteConfiguration || saving}
                  label="Ignore certificate errors"
                  onChange={(_, { checked }) =>
                    update('ignoreCertificateErrors', checked)
                  }
                />
              }
            />
          </Form.Group>
        </Form>

        <div className="integration-actions">
          <Popup
            content="Apply these FTP settings through the runtime configuration overlay."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'runtime'}
                onClick={applyRuntime}
                primary
              >
                <Icon name="save" />
                Apply Runtime
              </Button>
            }
          />
          <Popup
            content="Persist these FTP settings to the YAML configuration file."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'yaml'}
                onClick={saveYaml}
              >
                <Icon name="file alternate" />
                Save YAML
              </Button>
            }
          />
          <Popup
            content="Discard unsaved FTP edits and restore the values currently reported by the daemon."
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
      </Card.Content>
    </Card>
  );
};

const SourceFeedIntegrationsPanel = ({ options }) => {
  const remoteConfiguration = Boolean(
    getOption(options, 'remoteConfiguration', 'RemoteConfiguration'),
  );
  const [form, setForm] = useState(() => buildSourceFeedForm(options));
  const [savingAction, setSavingAction] = useState('');
  const [message, setMessage] = useState(null);
  const saving = Boolean(savingAction);

  useEffect(() => {
    setForm(buildSourceFeedForm(options));
  }, [options]);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const reset = () => {
    setForm(buildSourceFeedForm(options));
    setMessage(null);
  };

  const missingRequiredSettings = [
    form.spotifyEnabled &&
      !form.spotifyConfigured &&
      !form.spotifyClientId.trim() &&
      'Spotify needs a client ID before account connection or provider imports can run.',
    form.youTubeEnabled &&
      !form.youTubeConfigured &&
      !form.youTubeApiKey.trim() &&
      'YouTube needs a Data API key before playlist expansion can run.',
    form.lastFmEnabled &&
      !form.lastFmConfigured &&
      !form.lastFmApiKey.trim() &&
      'Last.fm needs an API key before loved/recent/top imports can run.',
  ].filter(Boolean);

  const buildOverlay = () => {
    const spotifyPatch = {
      enabled: form.spotifyEnabled,
      maxItemsPerImport: toNumber(form.spotifyMaxItems, 500),
      market: form.spotifyMarket.trim().toUpperCase(),
      redirectUri: form.spotifyRedirectUri.trim(),
      timeoutSeconds: toNumber(form.spotifyTimeout, 20),
    };

    if (form.spotifyClientId.trim()) {
      spotifyPatch.clientId = form.spotifyClientId.trim();
    }

    if (form.spotifyClientSecret.trim()) {
      spotifyPatch.clientSecret = form.spotifyClientSecret.trim();
    }

    const youTubePatch = {
      enabled: form.youTubeEnabled,
    };

    if (form.youTubeApiKey.trim()) {
      youTubePatch.apiKey = form.youTubeApiKey.trim();
    }

    const lastFmPatch = {
      enabled: form.lastFmEnabled,
    };

    if (form.lastFmApiKey.trim()) {
      lastFmPatch.apiKey = form.lastFmApiKey.trim();
    }

    return {
      integration: {
        lastFm: lastFmPatch,
        spotify: spotifyPatch,
        youTube: youTubePatch,
      },
    };
  };

  const markSecretsConfigured = (overlay) => {
    const spotifyPatch = overlay.integration.spotify;
    const youTubePatch = overlay.integration.youTube;
    const lastFmPatch = overlay.integration.lastFm;

    setForm((current) => ({
      ...current,
      lastFmApiKey: '',
      lastFmConfigured: current.lastFmConfigured || Boolean(lastFmPatch.apiKey),
      spotifyClientId: '',
      spotifyClientSecret: '',
      spotifyConfigured: current.spotifyConfigured || Boolean(spotifyPatch.clientId),
      spotifySecretConfigured:
        current.spotifySecretConfigured || Boolean(spotifyPatch.clientSecret),
      youTubeApiKey: '',
      youTubeConfigured: current.youTubeConfigured || Boolean(youTubePatch.apiKey),
    }));
  };

  const applyRuntime = async () => {
    setSavingAction('runtime');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      await optionsApi.applyOverlay(overlay);
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'Source-feed integration settings applied for this running daemon.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to apply source-feed integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  const saveYaml = async () => {
    setSavingAction('yaml');
    setMessage(null);
    const overlay = buildOverlay();

    try {
      const yaml = await optionsApi.getYaml();
      const document = YAML.parseDocument(yaml || '{}');
      const set = (path, value) => document.setIn(path, value);
      const spotifyPatch = overlay.integration.spotify;
      const youTubePatch = overlay.integration.youTube;
      const lastFmPatch = overlay.integration.lastFm;

      set(['integrations', 'spotify', 'enabled'], spotifyPatch.enabled);
      set(['integrations', 'spotify', 'redirect_uri'], spotifyPatch.redirectUri);
      set(['integrations', 'spotify', 'timeout_seconds'], spotifyPatch.timeoutSeconds);
      set(['integrations', 'spotify', 'max_items_per_import'], spotifyPatch.maxItemsPerImport);
      set(['integrations', 'spotify', 'market'], spotifyPatch.market);
      if (spotifyPatch.clientId) {
        set(['integrations', 'spotify', 'client_id'], spotifyPatch.clientId);
      }

      if (spotifyPatch.clientSecret) {
        set(['integrations', 'spotify', 'client_secret'], spotifyPatch.clientSecret);
      }

      set(['integrations', 'youtube', 'enabled'], youTubePatch.enabled);
      if (youTubePatch.apiKey) {
        set(['integrations', 'youtube', 'api_key'], youTubePatch.apiKey);
      }

      set(['integrations', 'lastfm', 'enabled'], lastFmPatch.enabled);
      if (lastFmPatch.apiKey) {
        set(['integrations', 'lastfm', 'api_key'], lastFmPatch.apiKey);
      }

      await optionsApi.updateYaml({ yaml: document.toString() });
      markSecretsConfigured(overlay);
      setMessage({
        positive: true,
        text: 'Source-feed integration settings saved to YAML.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to save source-feed integration settings.',
      });
    } finally {
      setSavingAction('');
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="rss" />
          Source Feed Imports
        </Card.Header>
        <Card.Meta>
          Provider settings for Wishlist Import Feed previews.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(form.spotifyEnabled, 'Spotify On', 'Spotify Off')}
          <Label>
            <Icon name={form.spotifyConfigured ? 'key' : 'warning sign'} />
            Spotify Client ID {form.spotifyConfigured ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.youTubeEnabled, 'YouTube On', 'YouTube Off')}
          <Label>
            <Icon name={form.youTubeConfigured ? 'key' : 'warning sign'} />
            YouTube API Key {form.youTubeConfigured ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.lastFmEnabled, 'Last.fm On', 'Last.fm Off')}
          <Label>
            <Icon name={form.lastFmConfigured ? 'key' : 'warning sign'} />
            Last.fm API Key {form.lastFmConfigured ? 'Configured' : 'Missing'}
          </Label>
        </div>

        {!remoteConfiguration && (
          <Message
            info
            size="small"
          >
            Runtime configuration changes are disabled. Enable remote
            configuration or edit YAML in the Options tab to change these
            provider settings.
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
        {missingRequiredSettings.length > 0 && (
          <Message
            size="small"
            warning
          >
            <Message.List items={missingRequiredSettings} />
          </Message>
        )}

        <Form className="source-feed-settings-form">
          <Segment>
            <Header as="h4">
              <Icon name="spotify" />
              Spotify
            </Header>
            <Popup
              content="Turns on Spotify source-feed imports and account connection. Private liked/saved/followed feeds still require a connected Spotify account or bearer token."
              trigger={
                <Checkbox
                  aria-label="Enable Spotify source-feed imports"
                  checked={form.spotifyEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Spotify imports"
                  onChange={(_, { checked }) => update('spotifyEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify client ID"
                disabled={!remoteConfiguration || saving}
                label="Client ID"
                onChange={(_, { value }) => update('spotifyClientId', value)}
                placeholder={form.spotifyConfigured ? 'Configured' : 'Spotify app client ID'}
                type="password"
                value={form.spotifyClientId}
              />
              <Form.Input
                aria-label="Spotify client secret"
                disabled={!remoteConfiguration || saving}
                label="Client Secret"
                onChange={(_, { value }) => update('spotifyClientSecret', value)}
                placeholder={
                  form.spotifySecretConfigured
                    ? 'Configured'
                    : 'Optional for OAuth; required for app-token public imports'
                }
                type="password"
                value={form.spotifyClientSecret}
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify redirect URI"
                disabled={!remoteConfiguration || saving}
                label="Redirect URI"
                onChange={(_, { value }) => update('spotifyRedirectUri', value)}
                placeholder="Infer from current host"
                value={form.spotifyRedirectUri}
              />
              <Form.Input
                aria-label="Spotify market"
                disabled={!remoteConfiguration || saving}
                label="Market"
                maxLength={2}
                onChange={(_, { value }) => update('spotifyMarket', value)}
                value={form.spotifyMarket}
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify timeout seconds"
                disabled={!remoteConfiguration || saving}
                label="Timeout Seconds"
                min={1}
                onChange={(_, { value }) => update('spotifyTimeout', value)}
                type="number"
                value={form.spotifyTimeout}
              />
              <Form.Input
                aria-label="Spotify max items per import"
                disabled={!remoteConfiguration || saving}
                label="Max Items Per Import"
                min={1}
                onChange={(_, { value }) => update('spotifyMaxItems', value)}
                type="number"
                value={form.spotifyMaxItems}
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="youtube play" />
              YouTube
            </Header>
            <Popup
              content="Turns on YouTube Data API playlist expansion for explicitly previewed Import Feed URLs."
              trigger={
                <Checkbox
                  aria-label="Enable YouTube playlist source-feed imports"
                  checked={form.youTubeEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable YouTube playlist expansion"
                  onChange={(_, { checked }) => update('youTubeEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Input
              aria-label="YouTube Data API key"
              disabled={!remoteConfiguration || saving}
              label="API Key"
              onChange={(_, { value }) => update('youTubeApiKey', value)}
              placeholder={form.youTubeConfigured ? 'Configured' : 'YouTube Data API key'}
              type="password"
              value={form.youTubeApiKey}
            />
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="lastfm" />
              Last.fm
            </Header>
            <Popup
              content="Turns on Last.fm API imports for explicitly previewed loved, recent, and top-track user URLs."
              trigger={
                <Checkbox
                  aria-label="Enable Last.fm source-feed imports"
                  checked={form.lastFmEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Last.fm imports"
                  onChange={(_, { checked }) => update('lastFmEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Input
              aria-label="Last.fm API key"
              disabled={!remoteConfiguration || saving}
              label="API Key"
              onChange={(_, { value }) => update('lastFmApiKey', value)}
              placeholder={form.lastFmConfigured ? 'Configured' : 'Last.fm API key'}
              type="password"
              value={form.lastFmApiKey}
            />
          </Segment>
        </Form>

        <div className="integration-actions">
          <Popup
            content="Apply these source-feed integration settings through the runtime configuration overlay."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'runtime'}
                onClick={applyRuntime}
                primary
              >
                <Icon name="save" />
                Apply Runtime
              </Button>
            }
          />
          <Popup
            content="Persist these source-feed integration settings to the YAML configuration file."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={savingAction === 'yaml'}
                onClick={saveYaml}
              >
                <Icon name="file alternate" />
                Save YAML
              </Button>
            }
          />
          <Popup
            content="Discard unsaved edits and restore the values currently reported by the daemon."
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
      </Card.Content>
    </Card>
  );
};

const VpnPanel = ({ options, state }) => {
  const vpnOptions = getVpnOptions(options);
  const vpnState = getVpnState(state);
  const gluetun = getOption(vpnOptions, 'gluetun', 'Gluetun') || {};
  const forwards = portForwards(vpnState);

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="shield alternate" />
          VPN
        </Card.Header>
        <Card.Meta>Daemon VPN readiness and configured provider settings.</Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(getOption(vpnOptions, 'enabled', 'Enabled'))}
          {boolLabel(
            getOption(vpnState, 'isReady', 'IsReady'),
            'Ready',
            'Not Ready',
          )}
          {boolLabel(
            getOption(vpnState, 'isConnected', 'IsConnected'),
            'Connected',
            'Disconnected',
          )}
          {boolLabel(
            getOption(vpnOptions, 'portForwarding', 'PortForwarding'),
            'Port Forwarding',
            'No Port Forwarding',
          )}
        </div>
        <Table
          basic="very"
          compact
          definition
        >
          <Table.Body>
            <Table.Row>
              <Table.Cell>Provider</Table.Cell>
              <Table.Cell>
                {getOption(gluetun, 'url', 'Url') ? 'Gluetun' : '-'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Control URL</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(gluetun, 'url', 'Url'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Polling Interval</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnOptions, 'pollingInterval', 'PollingInterval'))}
                {' ms'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Public IP</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnState, 'publicIPAddress', 'PublicIPAddress'))}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Location</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(vpnState, 'location', 'Location'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Forwarded Port</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnState, 'forwardedPort', 'ForwardedPort'))}
              </Table.Cell>
            </Table.Row>
          </Table.Body>
        </Table>
        {forwards.length > 0 && (
          <Table
            celled
            compact
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Slot</Table.HeaderCell>
                <Table.HeaderCell>Protocol</Table.HeaderCell>
                <Table.HeaderCell>Public</Table.HeaderCell>
                <Table.HeaderCell>Local</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {forwards.map((forward) => (
                <Table.Row key={`${forward.slot}-${forward.proto}-${forward.publicPort}`}>
                  <Table.Cell>{forward.slot}</Table.Cell>
                  <Table.Cell>{forward.proto}</Table.Cell>
                  <Table.Cell>
                    {valueOrDash(forward.publicIPAddress || forward.publicIp)}:
                    {forward.publicPort}
                  </Table.Cell>
                  <Table.Cell>
                    {forward.localPort || '-'}
                    {forward.targetPort && forward.targetPort !== forward.publicPort
                      ? ` -> ${forward.targetPort}`
                      : ''}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </Card.Content>
    </Card>
  );
};

const LidarrPanel = ({ options }) => {
  const lidarrOptions = getLidarrOptions(options);
  const [status, setStatus] = useState(null);
  const [wanted, setWanted] = useState([]);
  const [syncResult, setSyncResult] = useState(null);
  const [importDirectory, setImportDirectory] = useState('');
  const [importResult, setImportResult] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState('');
  const enabled = getOption(lidarrOptions, 'enabled', 'Enabled');

  const maskedApiKey = useMemo(() => {
    const apiKey = getOption(lidarrOptions, 'apiKey', 'ApiKey');
    return apiKey ? 'Configured' : 'Not configured';
  }, [lidarrOptions]);

  const run = async (name, action) => {
    setLoading(name);
    setError('');

    try {
      await action();
    } catch (error) {
      setError(
        error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Lidarr request failed',
      );
    } finally {
      setLoading('');
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="music" />
          Lidarr
        </Card.Header>
        <Card.Meta>Wanted-album sync and completed-download import bridge.</Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(enabled)}
          {boolLabel(
            getOption(lidarrOptions, 'syncWantedToWishlist', 'SyncWantedToWishlist'),
            'Wanted Sync',
            'Wanted Sync Off',
          )}
          {boolLabel(
            getOption(lidarrOptions, 'autoImportCompleted', 'AutoImportCompleted'),
            'Auto Import',
            'Auto Import Off',
          )}
          <Label>
            <Icon name={maskedApiKey === 'Configured' ? 'key' : 'warning sign'} />
            API Key {maskedApiKey}
          </Label>
        </div>
        <Table
          basic="very"
          compact
          definition
        >
          <Table.Body>
            <Table.Row>
              <Table.Cell>URL</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(lidarrOptions, 'url', 'Url'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Timeout</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'timeoutSeconds', 'TimeoutSeconds'))}
                {' s'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Sync Interval</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'syncIntervalSeconds', 'SyncIntervalSeconds'))}
                {' s'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Import Mode</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(lidarrOptions, 'importMode', 'ImportMode'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Import Path Map</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'importPathFrom', 'ImportPathFrom'))}
                {' -> '}
                {valueOrDash(getOption(lidarrOptions, 'importPathTo', 'ImportPathTo'))}
              </Table.Cell>
            </Table.Row>
          </Table.Body>
        </Table>
        {error && (
          <Message
            negative
            size="small"
          >
            {error}
          </Message>
        )}
        <div className="integration-actions">
          <Popup
            content="Fetch Lidarr system status using the configured URL and API key."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'status'}
                onClick={() =>
                  run('status', async () => setStatus(await lidarr.getStatus()))
                }
              >
                <Icon name="heartbeat" />
                Check Status
              </Button>
            }
          />
          <Popup
            content="Preview Lidarr wanted albums that can be synced into slskdN Wishlist."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'wanted'}
                onClick={() =>
                  run('wanted', async () =>
                    setWanted(await lidarr.getWantedMissing({ pageSize: 25 })),
                  )
                }
              >
                <Icon name="list" />
                Load Wanted
              </Button>
            }
          />
          <Popup
            content="Create or refresh slskdN Wishlist entries from Lidarr wanted albums."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'sync'}
                onClick={() =>
                  run('sync', async () => setSyncResult(await lidarr.syncWanted()))
                }
                primary
              >
                <Icon name="sync" />
                Sync Wanted
              </Button>
            }
          />
        </div>
        {status && (
          <Message
            positive
            size="small"
          >
            Lidarr responded: {status.appName || status.AppName || 'Lidarr'}{' '}
            {status.version || status.Version || ''}
          </Message>
        )}
        {syncResult && (
          <Message
            info
            size="small"
          >
            Wanted sync: {syncResult.createdCount ?? syncResult.CreatedCount ?? 0} created,{' '}
            {syncResult.duplicateCount ?? syncResult.DuplicateCount ?? 0} duplicates,{' '}
            {syncResult.skippedCount ?? syncResult.SkippedCount ?? 0} skipped.
          </Message>
        )}
        {wanted.length > 0 && (
          <Table
            celled
            compact
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Artist</Table.HeaderCell>
                <Table.HeaderCell>Album</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {wanted.slice(0, 10).map((album) => (
                <Table.Row key={album.id || album.Id || `${album.title}-${album.foreignAlbumId}`}>
                  <Table.Cell>
                    {album.artist?.artistName || album.Artist?.ArtistName || '-'}
                  </Table.Cell>
                  <Table.Cell>{album.title || album.Title || '-'}</Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
        <Segment className="integration-manual-import">
          <Header as="h4">Manual Import</Header>
          <Input
            action={{
              content: 'Import',
              disabled: !importDirectory.trim(),
              icon: 'download',
              loading: loading === 'import',
              onClick: () =>
                run('import', async () =>
                  setImportResult(
                    await lidarr.importCompletedDirectory({
                      directory: importDirectory.trim(),
                    }),
                  ),
                ),
            }}
            fluid
            onChange={(_, { value }) => setImportDirectory(value)}
            placeholder="Completed download directory visible to slskdN..."
            value={importDirectory}
          />
          {importResult && (
            <Message
              size="small"
              warning={Boolean(importResult.skippedReason || importResult.SkippedReason)}
            >
              {importResult.skippedReason || importResult.SkippedReason
                ? `Skipped: ${importResult.skippedReason || importResult.SkippedReason}`
                : `Queued Lidarr command ${importResult.commandId || importResult.CommandId || '-'}`}
            </Message>
          )}
        </Segment>
      </Card.Content>
    </Card>
  );
};

const MediaServerPanel = () => {
  const [localPath, setLocalPath] = useState('');
  const [serverPath, setServerPath] = useState('');
  const [remotePathFrom, setRemotePathFrom] = useState('');
  const [remotePathTo, setRemotePathTo] = useState('');
  const diagnostic = buildMediaServerPathDiagnostic({
    localPath,
    remotePathFrom,
    remotePathTo,
    serverPath,
  });

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="server" />
          Media Servers
        </Card.Header>
        <Card.Meta>
          Optional Plex, Jellyfin/Emby, and Navidrome integration planning and path diagnostics.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <Card.Group
          itemsPerRow={3}
          stackable
        >
          {mediaServerAdapters.map((adapter) => (
            <Card
              className="media-server-adapter-card"
              key={adapter.id}
            >
              <Card.Content>
                <Card.Header>{adapter.label}</Card.Header>
                <Card.Meta>
                  {adapter.requiresToken ? 'Token required' : 'No token required'}
                </Card.Meta>
                <div className="integration-status-row">
                  {adapter.capabilities.map((capability) => (
                    <Label
                      basic
                      key={capability}
                      size="tiny"
                    >
                      {capability}
                    </Label>
                  ))}
                </div>
              </Card.Content>
            </Card>
          ))}
        </Card.Group>

        <Segment className="integration-manual-import">
          <Header as="h4">Path Diagnostics</Header>
          <p>
            Check whether a completed file path reported by slskdN maps to the
            path a media server can scan.
          </p>
          <div className="media-server-path-grid">
            <Input
              aria-label="slskdN local file path"
              fluid
              label="slskdN path"
              onChange={(_, { value }) => setLocalPath(value)}
              placeholder="/downloads/complete/Artist/Album/track.flac"
              value={localPath}
            />
            <Input
              aria-label="Media server file path"
              fluid
              label="Server path"
              onChange={(_, { value }) => setServerPath(value)}
              placeholder="/library/music/Artist/Album/track.flac"
              value={serverPath}
            />
            <Input
              aria-label="Remote path map from"
              fluid
              label="Map from"
              onChange={(_, { value }) => setRemotePathFrom(value)}
              placeholder="/downloads/complete"
              value={remotePathFrom}
            />
            <Input
              aria-label="Remote path map to"
              fluid
              label="Map to"
              onChange={(_, { value }) => setRemotePathTo(value)}
              placeholder="/library/music"
              value={remotePathTo}
            />
          </div>
          <Message
            color={diagnostic.color}
            size="small"
          >
            <Message.Header>{diagnostic.status}</Message.Header>
            <p>{diagnostic.message}</p>
            {diagnostic.mappedPath && <p>Mapped path: {diagnostic.mappedPath}</p>}
          </Message>
        </Segment>
      </Card.Content>
    </Card>
  );
};

const ServarrReadinessPanel = ({ options }) => {
  const lidarrOptions = getLidarrOptions(options);
  const checks = buildServarrReadiness({
    apiKey: getOption(lidarrOptions, 'apiKey', 'ApiKey'),
    autoImportCompleted: getOption(
      lidarrOptions,
      'autoImportCompleted',
      'AutoImportCompleted',
    ),
    enabled: getOption(lidarrOptions, 'enabled', 'Enabled'),
    importPathFrom: getOption(lidarrOptions, 'importPathFrom', 'ImportPathFrom'),
    importPathTo: getOption(lidarrOptions, 'importPathTo', 'ImportPathTo'),
    syncWantedToWishlist: getOption(
      lidarrOptions,
      'syncWantedToWishlist',
      'SyncWantedToWishlist',
    ),
    url: getOption(lidarrOptions, 'url', 'Url'),
  });
  const summary = summarizeServarrReadiness(checks);

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="settings" />
          Servarr Setup
        </Card.Header>
        <Card.Meta>
          Local readiness checklist for indexer/download-client style integration.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          <Label color={summary.status === 'Ready' ? 'green' : 'orange'}>
            <Icon name={summary.status === 'Ready' ? 'check circle' : 'warning sign'} />
            {summary.status}
          </Label>
          <Label>
            {summary.ready}/{summary.total} checks ready
          </Label>
        </div>
        <Table
          celled
          compact
        >
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>Check</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
              <Table.HeaderCell>Why it matters</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {checks.map((check) => (
              <Table.Row key={check.id}>
                <Table.Cell>{check.title}</Table.Cell>
                <Table.Cell>
                  {boolLabel(check.ready, 'Ready', 'Needs Setup')}
                </Table.Cell>
                <Table.Cell>{check.description}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
        <Message
          info
          size="small"
        >
          This checklist is diagnostic only. It does not register indexers,
          create download clients, pull wanted items, or trigger imports.
        </Message>
      </Card.Content>
    </Card>
  );
};

const Integrations = ({ options = {}, state = {} }) => (
  <div className="integrations-admin">
    <Segment>
      <Header as="h3">
        <Icon name="plug" />
        Integrations
      </Header>
      <p>
        Operational status and admin actions for integrations that affect
        connection routing, downloads, and external media managers.
      </p>
    </Segment>
    <VpnPanel
      options={options}
      state={state}
    />
    <LidarrPanel options={options} />
    <NotificationIntegrationsPanel options={options} />
    <SourceFeedIntegrationsPanel options={options} />
    <FtpIntegrationPanel options={options} />
    <ServarrReadinessPanel options={options} />
    <MediaServerPanel />
  </div>
);

export default Integrations;
