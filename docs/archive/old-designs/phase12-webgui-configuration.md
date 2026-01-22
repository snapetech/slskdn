# Phase 12: WebGUI Configuration Reference

> **Document**: WebGUI settings panel specifications for adversarial resilience features  
> **Parent Doc**: `docs/phase12-adversarial-resilience-design.md`  
> **Tasks**: T-1202, T-1270, T-1271, T-1272, T-1273

---

## 1. Settings Panel Structure

### Location
`Settings → Privacy & Security` (new top-level settings section)

### Component Hierarchy
```
PrivacySettings.jsx
├── SecurityPresetSelector.jsx
├── TrafficAnalysisSection.jsx
│   ├── PaddingConfig.jsx
│   ├── TimingConfig.jsx
│   └── BatchingConfig.jsx
├── AnonymitySection.jsx
│   ├── TorConfig.jsx
│   ├── I2PConfig.jsx
│   └── RelayOnlyConfig.jsx
├── TransportSection.jsx
│   ├── TransportSelector.jsx
│   ├── Obfs4Config.jsx
│   └── MeekConfig.jsx
├── BridgeSection.jsx
│   ├── BridgeList.jsx
│   └── BridgeRequestButtons.jsx
├── RelayNodeSection.jsx
│   └── RelayConfig.jsx
└── DeniabilitySection.jsx (advanced, collapsed by default)
```

---

## 2. Security Presets

### Preset Definitions

```typescript
interface SecurityPreset {
  id: 'standard' | 'enhanced' | 'maximum' | 'custom';
  name: string;
  description: string;
  icon: string;
  settings: Partial<AdversarialOptions>;
}

const PRESETS: SecurityPreset[] = [
  {
    id: 'standard',
    name: 'Standard',
    description: 'Direct connections, no privacy overhead. Fastest performance.',
    icon: 'shield-outline',
    settings: {
      enabled: false
    }
  },
  {
    id: 'enhanced',
    name: 'Enhanced',
    description: 'Tor routing + message padding. Recommended for privacy.',
    icon: 'shield-half',
    settings: {
      enabled: true,
      privacy: {
        padding: { enabled: true, buckets: [512, 1024, 2048, 4096, 8192] },
        timing: { enabled: true, minJitterMs: 0, maxJitterMs: 300 },
        batching: { enabled: false }
      },
      anonymity: {
        mode: 'tor',
        tor: { enabled: true, streamIsolation: true }
      },
      transport: { primary: 'quic' }
    }
  },
  {
    id: 'maximum',
    name: 'Maximum',
    description: 'Full protection: onion routing, obfuscation, cover traffic. Slowest.',
    icon: 'shield',
    settings: {
      enabled: true,
      privacy: {
        padding: { enabled: true, buckets: [512, 1024, 2048, 4096, 8192, 16384] },
        timing: { enabled: true, minJitterMs: 50, maxJitterMs: 500 },
        batching: { enabled: true, flushIntervalMs: 2000 },
        coverTraffic: { enabled: true, intervalMs: 30000 }
      },
      anonymity: {
        mode: 'tor',
        tor: { enabled: true, streamIsolation: true, requireTor: true }
      },
      transport: { primary: 'obfs4' },
      onion: { enabled: true, defaultHops: 3 }
    }
  },
  {
    id: 'custom',
    name: 'Custom',
    description: 'Configure each setting individually.',
    icon: 'settings',
    settings: {} // User-configured
  }
];
```

### Preset Selector UI

```jsx
// SecurityPresetSelector.jsx
<Form.Field>
  <label>Security Level</label>
  <div className="preset-selector">
    {PRESETS.map(preset => (
      <div 
        key={preset.id}
        className={`preset-option ${selected === preset.id ? 'selected' : ''}`}
        onClick={() => onSelect(preset.id)}
      >
        <Icon name={preset.icon} />
        <div className="preset-content">
          <div className="preset-name">{preset.name}</div>
          <div className="preset-description">{preset.description}</div>
        </div>
        <Radio checked={selected === preset.id} />
      </div>
    ))}
  </div>
</Form.Field>
```

---

## 3. Section Specifications

### 3.1 Traffic Analysis Protection

```jsx
// TrafficAnalysisSection.jsx
<Accordion.Title active={expanded} onClick={toggle}>
  <Icon name="dropdown" />
  Traffic Analysis Protection
  <Label color={paddingEnabled || timingEnabled ? 'green' : 'grey'}>
    {paddingEnabled || timingEnabled ? 'Active' : 'Disabled'}
  </Label>
</Accordion.Title>
<Accordion.Content active={expanded}>
  {/* Message Padding */}
  <Form.Group>
    <Form.Checkbox
      label="Message Padding"
      checked={padding.enabled}
      onChange={handlePaddingToggle}
    />
    <Popup
      trigger={<Icon name="info circle" />}
      content="Pads all messages to fixed sizes to prevent observers from 
               inferring content type based on message size."
    />
  </Form.Group>
  
  {padding.enabled && (
    <Form.Field>
      <label>Bucket Sizes (bytes)</label>
      <Input 
        value={padding.buckets.join(', ')}
        placeholder="512, 1024, 2048, 4096, 8192"
        onChange={handleBucketsChange}
      />
      <small>Messages will be padded to the smallest bucket that fits.</small>
    </Form.Field>
  )}
  
  {/* Timing Jitter */}
  <Form.Group>
    <Form.Checkbox
      label="Timing Jitter"
      checked={timing.enabled}
      onChange={handleTimingToggle}
    />
    <Popup
      trigger={<Icon name="info circle" />}
      content="Adds random delays to outbound messages to prevent timing 
               correlation attacks."
    />
  </Form.Group>
  
  {timing.enabled && (
    <Form.Field>
      <label>Delay Range: {timing.minJitterMs}ms - {timing.maxJitterMs}ms</label>
      <Slider
        min={0}
        max={1000}
        value={[timing.minJitterMs, timing.maxJitterMs]}
        onChange={handleTimingRange}
      />
    </Form.Field>
  )}
  
  {/* Cover Traffic (Advanced) */}
  <Divider />
  <Form.Group>
    <Form.Checkbox
      label="Cover Traffic (Advanced)"
      checked={coverTraffic.enabled}
      onChange={handleCoverToggle}
    />
    <Popup
      trigger={<Icon name="info circle" color="orange" />}
      content="Sends dummy messages when idle to make traffic patterns constant.
               Increases bandwidth usage but improves privacy."
    />
  </Form.Group>
  
  {coverTraffic.enabled && (
    <Form.Field>
      <label>Interval: {coverTraffic.intervalMs / 1000} seconds</label>
      <Slider
        min={5000}
        max={120000}
        step={5000}
        value={coverTraffic.intervalMs}
        onChange={handleCoverInterval}
      />
    </Form.Field>
  )}
</Accordion.Content>
```

### 3.2 IP Anonymization (Tor/I2P)

```jsx
// AnonymitySection.jsx
<Accordion.Title active={expanded} onClick={toggle}>
  <Icon name="dropdown" />
  IP Anonymization
  <Label color={tor.enabled ? 'green' : 'grey'}>
    {tor.enabled ? `Via ${anonymity.mode.toUpperCase()}` : 'Direct'}
  </Label>
</Accordion.Title>
<Accordion.Content active={expanded}>
  {/* Transport Mode */}
  <Form.Field>
    <label>Anonymity Transport</label>
    <Dropdown
      selection
      options={[
        { key: 'direct', value: 'direct', text: 'Direct (No Anonymization)' },
        { key: 'tor', value: 'tor', text: 'Tor SOCKS5 Proxy' },
        { key: 'i2p', value: 'i2p', text: 'I2P (SAM Bridge)' },
        { key: 'relay_only', value: 'relay_only', text: 'Relay Only (Mesh Relays)' }
      ]}
      value={anonymity.mode}
      onChange={handleModeChange}
    />
  </Form.Field>
  
  {/* Tor Settings */}
  {anonymity.mode === 'tor' && (
    <Segment>
      <Header as="h4">
        <Icon name="user secret" />
        Tor Settings
      </Header>
      
      <Form.Group widths="equal">
        <Form.Input
          label="SOCKS Address"
          value={tor.socksHost}
          onChange={handleTorHost}
          placeholder="127.0.0.1"
        />
        <Form.Input
          label="Port"
          type="number"
          value={tor.socksPort}
          onChange={handleTorPort}
          placeholder="9050"
        />
      </Form.Group>
      
      <Form.Checkbox
        label="Stream Isolation (use different circuit per peer)"
        checked={tor.streamIsolation}
        onChange={handleStreamIsolation}
      />
      
      <Form.Checkbox
        label="Require Tor (block all traffic if Tor unavailable)"
        checked={tor.requireTor}
        onChange={handleRequireTor}
      />
      
      {/* Connection Status */}
      <Message 
        icon
        color={torStatus.connected ? 'green' : 'red'}
      >
        <Icon name={torStatus.connected ? 'check circle' : 'warning circle'} />
        <Message.Content>
          <Message.Header>
            {torStatus.connected ? 'Connected' : 'Disconnected'}
          </Message.Header>
          {torStatus.connected 
            ? `Circuit established via ${torStatus.exitCountry}`
            : torStatus.error || 'Tor not available'
          }
        </Message.Content>
      </Message>
    </Segment>
  )}
  
  {/* I2P Settings */}
  {anonymity.mode === 'i2p' && (
    <Segment>
      <Header as="h4">
        <Icon name="hide" />
        I2P Settings
      </Header>
      
      <Form.Group widths="equal">
        <Form.Input
          label="SAM Bridge Address"
          value={i2p.samHost}
          placeholder="127.0.0.1"
        />
        <Form.Input
          label="Port"
          type="number"
          value={i2p.samPort}
          placeholder="7656"
        />
      </Form.Group>
      
      <Form.Field>
        <label>Tunnel Length: {i2p.tunnelLength} hops</label>
        <Slider
          min={1}
          max={5}
          value={i2p.tunnelLength}
          onChange={handleTunnelLength}
        />
        <small>More hops = more privacy, higher latency</small>
      </Form.Field>
    </Segment>
  )}
</Accordion.Content>
```

### 3.3 Obfuscated Transports

```jsx
// TransportSection.jsx
<Accordion.Title active={expanded} onClick={toggle}>
  <Icon name="dropdown" />
  Obfuscated Transports
  <Label color={transport.primary !== 'quic' ? 'green' : 'grey'}>
    {transport.primary.toUpperCase()}
  </Label>
</Accordion.Title>
<Accordion.Content active={expanded}>
  <Form.Field>
    <label>Primary Transport</label>
    <Dropdown
      selection
      options={[
        { key: 'quic', value: 'quic', text: 'QUIC (Standard, Encrypted)' },
        { key: 'websocket', value: 'websocket', text: 'WebSocket (Looks like Web Traffic)' },
        { key: 'http_tunnel', value: 'http_tunnel', text: 'HTTP Tunnel (Looks like API Traffic)' },
        { key: 'obfs4', value: 'obfs4', text: 'obfs4 (Tor-style Obfuscation)' },
        { key: 'meek', value: 'meek', text: 'Meek (CDN-based)' }
      ]}
      value={transport.primary}
      onChange={handleTransportChange}
    />
  </Form.Field>
  
  {/* obfs4 Configuration */}
  {transport.primary === 'obfs4' && (
    <Segment>
      <Header as="h4">obfs4 Bridges</Header>
      <p>
        obfs4 makes traffic look like random noise, resisting deep packet inspection.
        You need bridge addresses to use this transport.
      </p>
      
      <Form.TextArea
        label="Bridge Lines"
        placeholder="obfs4 192.0.2.1:443 cert=... iat-mode=0
obfs4 192.0.2.2:443 cert=... iat-mode=0"
        value={obfs4.bridges.join('\n')}
        onChange={handleBridgesChange}
        rows={4}
      />
      
      <Button.Group>
        <Button onClick={requestBridgesEmail}>
          <Icon name="mail" />
          Request via Email
        </Button>
        <Button onClick={scanQRCode}>
          <Icon name="qrcode" />
          Scan QR Code
        </Button>
      </Button.Group>
      
      <Divider hidden />
      
      <Form.Input
        label="obfs4proxy Path"
        placeholder="/usr/bin/obfs4proxy"
        value={obfs4.binaryPath}
        onChange={handleBinaryPath}
      />
    </Segment>
  )}
  
  {/* Meek Configuration */}
  {transport.primary === 'meek' && (
    <Segment>
      <Header as="h4">Meek (CDN) Settings</Header>
      <p>
        Routes traffic through major CDNs. Blocking requires blocking the entire CDN.
      </p>
      
      <Form.Input
        label="Front Domain"
        placeholder="ajax.aspnetcdn.com"
        value={meek.frontDomain}
        onChange={handleFrontDomain}
      />
      
      <Form.Input
        label="Relay URL"
        placeholder="https://meek-relay.example.com/"
        value={meek.relayUrl}
        onChange={handleRelayUrl}
      />
    </Segment>
  )}
  
  {/* Domain Fronting */}
  <Divider />
  <Form.Checkbox
    label="Domain Fronting (Advanced)"
    checked={domainFronting.enabled}
    onChange={handleDomainFrontingToggle}
  />
  
  {domainFronting.enabled && (
    <Form.Group widths="equal">
      <Form.Input
        label="Front Domain (TLS SNI)"
        placeholder="cdn.example.com"
        value={domainFronting.frontDomain}
      />
      <Form.Input
        label="Host Header"
        placeholder="mesh.slskdn.org"
        value={domainFronting.hostHeader}
      />
    </Form.Group>
  )}
</Accordion.Content>
```

### 3.4 Relay Node (Volunteer)

```jsx
// RelayNodeSection.jsx
<Accordion.Title active={expanded} onClick={toggle}>
  <Icon name="dropdown" />
  Relay Node
  <Label color={relay.enabled ? 'blue' : 'grey'}>
    {relay.enabled ? 'Contributing' : 'Not Enabled'}
  </Label>
</Accordion.Title>
<Accordion.Content active={expanded}>
  <Message info>
    <Message.Header>Help Users in Censored Regions</Message.Header>
    <p>
      By enabling relay mode, you volunteer bandwidth to help users who can't 
      connect directly. Your node will forward encrypted traffic without 
      seeing its contents.
    </p>
  </Message>
  
  <Form.Checkbox
    label="Enable Relay Node"
    checked={relay.enabled}
    onChange={handleRelayToggle}
  />
  
  {relay.enabled && (
    <>
      <Form.Field>
        <label>Max Bandwidth: {relay.maxBandwidthMbps} Mbps</label>
        <Slider
          min={1}
          max={100}
          value={relay.maxBandwidthMbps}
          onChange={handleBandwidth}
        />
      </Form.Field>
      
      <Form.Field>
        <label>Max Concurrent Circuits: {relay.maxCircuits}</label>
        <Slider
          min={10}
          max={500}
          value={relay.maxCircuits}
          onChange={handleMaxCircuits}
        />
      </Form.Field>
      
      <Message warning>
        <Icon name="warning sign" />
        <Message.Content>
          <Message.Header>Legal Notice</Message.Header>
          Running a relay may have legal implications in your jurisdiction. 
          You are forwarding traffic for others. Review your local laws before enabling.
        </Message.Content>
      </Message>
    </>
  )}
</Accordion.Content>
```

### 3.5 Bridge Configuration

```jsx
// BridgeSection.jsx
<Accordion.Title active={expanded} onClick={toggle}>
  <Icon name="dropdown" />
  Bridges (Censorship Circumvention)
  <Label color={bridges.enabled ? 'green' : 'grey'}>
    {bridges.enabled ? `${bridges.sources.length} configured` : 'Disabled'}
  </Label>
</Accordion.Title>
<Accordion.Content active={expanded}>
  <Message info>
    <Message.Header>What are Bridges?</Message.Header>
    <p>
      Bridges are unlisted entry points to the network. If direct connections 
      are blocked in your region, bridges can help you connect.
    </p>
  </Message>
  
  <Form.Checkbox
    label="Enable Bridge Mode"
    checked={bridges.enabled}
    onChange={handleBridgeToggle}
  />
  
  {bridges.enabled && (
    <>
      <Form.TextArea
        label="Bridge Addresses"
        placeholder="Paste bridge lines here, one per line..."
        value={bridgeText}
        onChange={handleBridgeText}
        rows={6}
      />
      
      <Header as="h5">Get Bridges</Header>
      <Button.Group vertical fluid>
        <Button onClick={() => window.open('https://bridges.slskdn.org/get')}>
          <Icon name="globe" />
          Request from Web
        </Button>
        <Button onClick={requestViaEmail}>
          <Icon name="mail" />
          Request via Email
        </Button>
        <Button onClick={scanQR}>
          <Icon name="qrcode" />
          Scan QR Code
        </Button>
      </Button.Group>
      
      {/* Bridge Status */}
      <Divider />
      <Header as="h5">Bridge Status</Header>
      <Table compact>
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell>Address</Table.HeaderCell>
            <Table.HeaderCell>Type</Table.HeaderCell>
            <Table.HeaderCell>Status</Table.HeaderCell>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {bridges.sources.map(bridge => (
            <Table.Row key={bridge.address}>
              <Table.Cell>{bridge.address}</Table.Cell>
              <Table.Cell>{bridge.transport}</Table.Cell>
              <Table.Cell>
                <Icon 
                  name={bridge.healthy ? 'check' : 'times'} 
                  color={bridge.healthy ? 'green' : 'red'} 
                />
                {bridge.healthy ? 'Working' : 'Failed'}
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </>
  )}
</Accordion.Content>
```

---

## 4. Privacy Dashboard

### Dashboard Layout

```jsx
// PrivacyDashboard.jsx
<Segment>
  <Header as="h2">
    <Icon name="shield" />
    Privacy Status
  </Header>
  
  {/* Protection Level Indicator */}
  <Progress
    percent={protectionLevel}
    color={protectionLevel > 80 ? 'green' : protectionLevel > 50 ? 'yellow' : 'red'}
    progress
  >
    {protectionLevel}% Protection ({presetName})
  </Progress>
  
  {/* Status Cards */}
  <Card.Group itemsPerRow={4}>
    <Card color={ipHidden ? 'green' : 'red'}>
      <Card.Content>
        <Icon name={ipHidden ? 'check' : 'times'} size="large" />
        <Card.Header>IP Hidden</Card.Header>
        <Card.Description>
          {ipHidden ? `via ${anonymityMode}` : 'Direct connections'}
        </Card.Description>
      </Card.Content>
    </Card>
    
    <Card color={trafficPadded ? 'green' : 'red'}>
      <Card.Content>
        <Icon name={trafficPadded ? 'check' : 'times'} size="large" />
        <Card.Header>Traffic Padded</Card.Header>
        <Card.Description>
          {trafficPadded ? paddingBuckets : 'Not enabled'}
        </Card.Description>
      </Card.Content>
    </Card>
    
    <Card color={timingJittered ? 'green' : 'red'}>
      <Card.Content>
        <Icon name={timingJittered ? 'check' : 'times'} size="large" />
        <Card.Header>Timing Jittered</Card.Header>
        <Card.Description>
          {timingJittered ? `${minJitter}-${maxJitter}ms` : 'Not enabled'}
        </Card.Description>
      </Card.Content>
    </Card>
    
    <Card color={censorshipResistant ? 'green' : 'red'}>
      <Card.Content>
        <Icon name={censorshipResistant ? 'check' : 'times'} size="large" />
        <Card.Header>Censorship Resistant</Card.Header>
        <Card.Description>
          {censorshipResistant ? transportMode : 'Standard transport'}
        </Card.Description>
      </Card.Content>
    </Card>
  </Card.Group>
  
  {/* Circuit Visualization (if onion routing enabled) */}
  {onionEnabled && (
    <Segment>
      <Header as="h4">Active Circuit</Header>
      <CircuitVisualization hops={circuitHops} />
    </Segment>
  )}
  
  {/* Recent Activity */}
  <Segment>
    <Header as="h4">Recent Activity</Header>
    <Feed>
      {recentEvents.map(event => (
        <Feed.Event key={event.id}>
          <Feed.Label icon={event.icon} />
          <Feed.Content>
            <Feed.Date>{event.time}</Feed.Date>
            <Feed.Summary>{event.message}</Feed.Summary>
          </Feed.Content>
        </Feed.Event>
      ))}
    </Feed>
  </Segment>
  
  {/* Recommendations */}
  {recommendations.length > 0 && (
    <Message warning>
      <Message.Header>Recommendations</Message.Header>
      <Message.List>
        {recommendations.map((rec, i) => (
          <Message.Item key={i}>{rec}</Message.Item>
        ))}
      </Message.List>
    </Message>
  )}
</Segment>
```

---

## 5. API Endpoints

### Settings API

```csharp
// AdversarialController.cs

[ApiController]
[Route("api/v0/adversarial")]
public class AdversarialController : ControllerBase
{
    /// <summary>
    /// Get current adversarial settings.
    /// </summary>
    [HttpGet("settings")]
    public ActionResult<AdversarialOptions> GetSettings();
    
    /// <summary>
    /// Update adversarial settings.
    /// </summary>
    [HttpPut("settings")]
    public ActionResult UpdateSettings([FromBody] AdversarialOptions options);
    
    /// <summary>
    /// Apply a preset.
    /// </summary>
    [HttpPost("settings/preset/{presetId}")]
    public ActionResult ApplyPreset(string presetId);
    
    /// <summary>
    /// Get Tor connection status.
    /// </summary>
    [HttpGet("status/tor")]
    public ActionResult<TorStatus> GetTorStatus();
    
    /// <summary>
    /// Get bridge health status.
    /// </summary>
    [HttpGet("status/bridges")]
    public ActionResult<IEnumerable<BridgeStatus>> GetBridgeStatus();
    
    /// <summary>
    /// Get active circuit information.
    /// </summary>
    [HttpGet("status/circuit")]
    public ActionResult<CircuitInfo> GetCircuitInfo();
    
    /// <summary>
    /// Get privacy dashboard summary.
    /// </summary>
    [HttpGet("dashboard")]
    public ActionResult<PrivacyDashboard> GetDashboard();
    
    /// <summary>
    /// Test Tor connectivity.
    /// </summary>
    [HttpPost("test/tor")]
    public async Task<ActionResult<TorTestResult>> TestTorConnection();
    
    /// <summary>
    /// Test bridge connectivity.
    /// </summary>
    [HttpPost("test/bridge")]
    public async Task<ActionResult<BridgeTestResult>> TestBridge([FromBody] string bridgeLine);
}
```

### Response Models

```csharp
public class TorStatus
{
    public bool Connected { get; set; }
    public string? ExitCountry { get; set; }
    public string? CircuitId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
}

public class BridgeStatus
{
    public string Address { get; set; }
    public string Transport { get; set; }
    public bool Healthy { get; set; }
    public DateTimeOffset? LastChecked { get; set; }
    public string? Error { get; set; }
}

public class CircuitInfo
{
    public string CircuitId { get; set; }
    public IReadOnlyList<CircuitHop> Hops { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int LatencyMs { get; set; }
}

public class CircuitHop
{
    public string RelayId { get; set; }
    public string? Country { get; set; }
    public int LatencyMs { get; set; }
}

public class PrivacyDashboard
{
    public int ProtectionLevel { get; set; }  // 0-100
    public string PresetName { get; set; }
    public bool IpHidden { get; set; }
    public string? AnonymityMode { get; set; }
    public bool TrafficPadded { get; set; }
    public bool TimingJittered { get; set; }
    public bool CensorshipResistant { get; set; }
    public IReadOnlyList<PrivacyEvent> RecentEvents { get; set; }
    public IReadOnlyList<string> Recommendations { get; set; }
}
```

---

## 6. Configuration File Format

### appsettings.yml

```yaml
# Adversarial Resilience & Privacy Settings
# ALL FEATURES DISABLED BY DEFAULT

adversarial:
  # Master enable switch
  enabled: false
  
  # Preset: "standard", "enhanced", "maximum", "custom"
  preset: "standard"
  
  # Privacy layer (traffic analysis protection)
  privacy:
    padding:
      enabled: false
      buckets: [512, 1024, 2048, 4096, 8192, 16384]
      random_fill: true
    timing:
      enabled: false
      min_jitter_ms: 0
      max_jitter_ms: 500
    batching:
      enabled: false
      flush_interval_ms: 2000
      max_batch_size: 10
    cover_traffic:
      enabled: false
      interval_ms: 30000
  
  # Anonymity layer (IP protection)
  anonymity:
    mode: "direct"  # "direct", "tor", "i2p", "relay_only"
    tor:
      enabled: false
      socks_host: "127.0.0.1"
      socks_port: 9050
      stream_isolation: true
      require_tor: false
    i2p:
      enabled: false
      sam_host: "127.0.0.1"
      sam_port: 7656
      tunnel_length: 3
    relay_only:
      enabled: false
      min_relays: 2
      trusted_relays: []
  
  # Obfuscated transports
  transport:
    primary: "quic"  # "quic", "websocket", "http_tunnel", "obfs4", "meek"
    websocket:
      enabled: false
      path: "/ws/mesh"
    obfs4:
      enabled: false
      binary_path: "/usr/bin/obfs4proxy"
      bridges: []
    meek:
      enabled: false
      front_domain: ""
      relay_url: ""
    domain_fronting:
      enabled: false
      front_domain: ""
      host_header: ""
  
  # Native onion routing
  onion:
    enabled: false
    default_hops: 3
    circuit_lifetime_minutes: 10
    relay_selection:
      prefer_diverse_asn: true
      avoid_same_country: true
  
  # Relay node (volunteer)
  relay:
    enabled: false
    max_bandwidth_mbps: 10
    max_circuits: 100
    allow_exit: false
  
  # Bridges (censorship circumvention)
  bridges:
    enabled: false
    sources: []
  
  # Deniable storage
  deniability:
    storage:
      enabled: false
```

---

## 7. Localization Keys

```json
{
  "privacy.title": "Privacy & Security",
  "privacy.preset.standard": "Standard",
  "privacy.preset.enhanced": "Enhanced",
  "privacy.preset.maximum": "Maximum",
  "privacy.preset.custom": "Custom",
  
  "privacy.section.traffic": "Traffic Analysis Protection",
  "privacy.section.anonymity": "IP Anonymization",
  "privacy.section.transport": "Obfuscated Transports",
  "privacy.section.bridges": "Bridges",
  "privacy.section.relay": "Relay Node",
  "privacy.section.deniability": "Plausible Deniability",
  
  "privacy.padding.label": "Message Padding",
  "privacy.padding.description": "Pads messages to fixed sizes to prevent size-based fingerprinting",
  
  "privacy.timing.label": "Timing Jitter",
  "privacy.timing.description": "Adds random delays to prevent timing correlation attacks",
  
  "privacy.tor.connected": "Connected",
  "privacy.tor.disconnected": "Disconnected",
  "privacy.tor.circuit": "Circuit established via {country}",
  
  "privacy.bridge.healthy": "Working",
  "privacy.bridge.failed": "Failed",
  
  "privacy.relay.warning": "Running a relay may have legal implications in your jurisdiction.",
  
  "privacy.dashboard.protection": "{level}% Protection ({preset})",
  "privacy.dashboard.ip_hidden": "IP Hidden",
  "privacy.dashboard.traffic_padded": "Traffic Padded",
  "privacy.dashboard.timing_jittered": "Timing Jittered",
  "privacy.dashboard.censorship_resistant": "Censorship Resistant"
}
```

---

*Document Version: 1.0*  
*Last Updated: December 10, 2025*

