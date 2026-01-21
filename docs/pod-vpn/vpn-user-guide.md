# slskdN Pod-Scoped Private Service Network (VPN-like Utility)

## Overview

The slskdN Pod-Scoped Private Service Network provides a secure, pod-based VPN-like utility that enables pod members to access private services through encrypted mesh tunnels. This feature allows trusted pod members to securely connect to services running on other pod members' machines without exposing those services to the public internet.

## Key Features

- **Pod-Scoped Access**: Services are only accessible to verified pod members
- **Encrypted Tunnels**: All traffic is encrypted end-to-end through the mesh
- **Destination Allowlisting**: Strict control over which destinations can be accessed
- **Rate Limiting**: Protection against abuse with configurable rate limits
- **DNS Security**: Protection against DNS rebinding attacks
- **Resource Governance**: Configurable limits on concurrent connections and bandwidth
- **Audit Logging**: Comprehensive logging of all tunnel activity

## Security Model

### Threat Model

The VPN feature addresses the following security concerns:

1. **Unauthorized Access**: Only pod members can access VPN services
2. **Traffic Interception**: All traffic is encrypted through the mesh overlay
3. **DNS Rebinding**: DNS resolution is validated and cached to prevent attacks
4. **Resource Exhaustion**: Rate limiting and connection limits prevent DoS attacks
5. **Destination Abuse**: Strict allowlisting prevents access to unauthorized services
6. **Information Leakage**: Comprehensive logging and audit trails

### Security Properties

- **Zero Trust**: Deny-all by default with explicit allowlisting
- **Defense in Depth**: Multiple validation layers and security controls
- **Principle of Least Privilege**: Minimal required permissions for access
- **Fail-Safe Defaults**: Secure defaults that require explicit configuration
- **Audit Compliance**: Detailed logging for security monitoring and compliance

## Architecture

### Components

1. **Private Gateway Mesh Service**: Core service handling tunnel creation and management
2. **Local Port Forwarder**: Client-side component for establishing local port forwarding
3. **DNS Security Service**: Validates DNS resolution and prevents rebinding attacks
4. **IP Range Classifier**: Classifies IP addresses for security validation
5. **Pod Policy Engine**: Enforces pod-level security policies

### Data Flow

1. Client requests tunnel to destination through pod gateway
2. Gateway validates request against pod policies and allowlists
3. DNS resolution is performed and cached with security validation
4. Gateway establishes outbound connection to destination
5. Encrypted tunnel is established through mesh overlay
6. Client can access destination through local port forwarding

## Configuration

### Pod Policy Configuration

VPN functionality is controlled through pod policies. To enable VPN features for a pod:

```json
{
  "capabilities": ["PrivateServiceGateway"],
  "privateServicePolicy": {
    "enabled": true,
    "maxMembers": 3,
    "gatewayPeerId": "peer-designated-as-gateway",
    "allowPrivateRanges": true,
    "allowPublicDestinations": false,
    "allowedDestinations": [
      {
        "hostPattern": "*.internal.company.com",
        "port": 443,
        "protocol": "tcp"
      },
      {
        "hostPattern": "192.168.1.100",
        "port": 80,
        "protocol": "tcp"
      }
    ],
    "registeredServices": [
      {
        "name": "Company Database",
        "description": "PostgreSQL database server",
        "kind": "Database",
        "destinationHost": "db.internal.company.com",
        "destinationPort": 5432,
        "protocol": "tcp"
      }
    ],
    "maxConcurrentTunnelsPerPeer": 5,
    "maxConcurrentTunnelsPod": 15,
    "maxNewTunnelsPerMinutePerPeer": 10,
    "maxBytesPerDayPerPeer": 1073741824,
    "idleTimeout": "01:00:00",
    "maxLifetime": "24:00:00",
    "dialTimeout": "00:00:30"
  }
}
```

### Policy Parameters

#### Capability Requirements
- `PrivateServiceGateway`: Must be included in pod capabilities to enable VPN

#### Basic Settings
- `enabled`: Must be `true` to activate VPN functionality
- `maxMembers`: Maximum pod members (hard limit of 3 for VPN pods)
- `gatewayPeerId`: Peer ID of the designated gateway node

#### Network Access Control
- `allowPrivateRanges`: Allow access to private IP ranges (RFC1918, ULA)
- `allowPublicDestinations`: Allow access to public internet destinations
- `allowedDestinations`: List of explicitly allowed host/port combinations
- `registeredServices`: Named services with metadata

#### Resource Limits
- `maxConcurrentTunnelsPerPeer`: Maximum simultaneous tunnels per pod member
- `maxConcurrentTunnelsPod`: Maximum simultaneous tunnels for entire pod
- `maxNewTunnelsPerMinutePerPeer`: Rate limit for new tunnel creation
- `maxBytesPerDayPerPeer`: Daily bandwidth limit per peer

#### Timeout Settings
- `idleTimeout`: Close tunnels after this period of inactivity
- `maxLifetime`: Maximum duration a tunnel can remain open
- `dialTimeout`: Timeout for establishing outbound connections

### Destination Allowlisting

#### Host Patterns
- Exact matches: `api.example.com`
- Wildcard patterns: `*.example.com`, `api.*.example.com`
- IP addresses: `192.168.1.100`, `10.0.0.0`
- IPv6 addresses: `2001:db8::1`, `fc00::1`

#### Security Restrictions
- Broad wildcards like `*.*` are rejected for security
- Only single-level wildcards are allowed (e.g., `*.domain.com`)
- Host patterns are case-insensitive
- Invalid characters are rejected

#### Blocked Addresses
The following address ranges are automatically blocked:
- Loopback addresses: `127.0.0.0/8`, `::1`
- Link-local addresses: `169.254.0.0/16`, `fe80::/10`
- Multicast addresses: `224.0.0.0/4`, `ff00::/8`
- Cloud metadata services: `169.254.169.254` (AWS, GCP, Azure)

### Registered Services

Pre-approved services can be registered for easy reference:

```json
{
  "name": "Production Database",
  "description": "Main PostgreSQL cluster",
  "kind": "Database",
  "destinationHost": "db-prod.company.internal",
  "destinationPort": 5432,
  "protocol": "tcp"
}
```

Registered services can be referenced by name when creating tunnels, providing better audit trails and policy management.

## Usage

### Creating VPN-Enabled Pods

1. **Create Pod with VPN Capability**:
   ```bash
   curl -X POST http://localhost:5000/api/v0/pods \
     -H "Content-Type: application/json" \
     -d '{
       "podId": "secure-services",
       "name": "Secure Services Pod",
       "capabilities": ["PrivateServiceGateway"],
       "privateServicePolicy": {
         "enabled": true,
         "maxMembers": 3,
         "gatewayPeerId": "my-peer-id",
         "allowPrivateRanges": true,
         "allowedDestinations": [
           {"hostPattern": "*.internal.company.com", "port": 443, "protocol": "tcp"}
         ]
       }
     }'
   ```

2. **Join Pod as Member**:
   ```bash
   curl -X POST http://localhost:5000/api/v0/pods/secure-services/members \
     -H "Content-Type: application/json" \
     -d '{"peerId": "member-peer-id"}'
   ```

### Establishing Tunnels

#### Using the WebGUI

1. Navigate to the Pods section
2. Select your VPN-enabled pod
3. Click the "Port Forwarding" tab
4. Click "Start New Forwarder"
5. Select destination from allowed services or enter custom destination
6. Specify local port for forwarding
7. Click "Start Forwarding"

#### Using the API

1. **Start Port Forwarding**:
   ```bash
   curl -X POST http://localhost:5000/api/v0/port-forwarding/start \
     -H "Content-Type: application/json" \
     -d '{
       "podId": "secure-services",
       "localPort": 8080,
       "destinationHost": "web.internal.company.com",
       "destinationPort": 80,
       "serviceName": "Company Website"
     }'
   ```

2. **Monitor Active Tunnels**:
   ```bash
   curl http://localhost:5000/api/v0/port-forwarding/status
   ```

3. **Stop Port Forwarding**:
   ```bash
   curl -X POST http://localhost:5000/api/v0/port-forwarding/stop/8080
   ```

### Accessing Services

Once a tunnel is established, access the service through localhost:

```bash
# Access web service through tunnel
curl http://localhost:8080

# Connect to database through tunnel
psql -h localhost -p 5432 -U myuser mydatabase

# SSH through tunnel
ssh -p 2222 user@localhost
```

## Security Considerations

### Authentication & Authorization

- All tunnel requests are validated against pod membership
- Gateway peer designation prevents unauthorized tunnel creation
- Request binding with nonces prevents replay attacks
- Peer identity verification through mesh overlay

### Network Security

- DNS rebinding protection through IP validation and caching
- Blocked address ranges prevent access to sensitive services
- Private range enforcement prevents public internet access
- Connection timeouts prevent resource exhaustion

### Resource Protection

- Rate limiting prevents DoS attacks
- Connection limits prevent resource exhaustion
- Bandwidth quotas ensure fair resource allocation
- Automatic cleanup of idle and expired tunnels

### Audit & Compliance

- Comprehensive logging of all tunnel operations
- Audit trails for security monitoring and compliance
- Structured logging with security-relevant information
- No payload logging to protect sensitive data

## Troubleshooting

### Common Issues

#### Tunnel Creation Fails

**Symptoms**: `OpenTunnel` requests are rejected

**Possible Causes**:
- Pod not configured with `PrivateServiceGateway` capability
- Destination not in allowlist
- Rate limits exceeded
- Gateway peer not designated or unavailable

**Solutions**:
1. Verify pod configuration includes VPN capability
2. Check destination is in `allowedDestinations` or `registeredServices`
3. Wait for rate limits to reset or reduce tunnel creation frequency
4. Ensure designated gateway peer is online and properly configured

#### DNS Resolution Issues

**Symptoms**: Connection fails with DNS-related errors

**Possible Causes**:
- DNS server unavailable
- Hostname resolution fails
- DNS rebinding protection triggered

**Solutions**:
1. Verify DNS configuration and connectivity
2. Check hostname is resolvable from gateway node
3. Review DNS security logs for blocked resolutions
4. Consider using IP addresses instead of hostnames

#### Connection Timeouts

**Symptoms**: Tunnels establish but connections timeout

**Possible Causes**:
- Destination service not running
- Firewall blocking connections
- Network connectivity issues
- Dial timeout too short

**Solutions**:
1. Verify destination service is running and accessible
2. Check firewall rules on destination host
3. Test network connectivity between gateway and destination
4. Adjust `dialTimeout` in pod policy if needed

#### Port Forwarding Issues

**Symptoms**: Local port forwarding doesn't work

**Possible Causes**:
- Local port already in use
- Insufficient permissions for port binding
- Firewall blocking local connections

**Solutions**:
1. Choose different local port
2. Run client with appropriate permissions
3. Configure firewall to allow local connections
4. Check for conflicting services using the same port

### Monitoring & Diagnostics

#### Log Analysis

Enable debug logging to troubleshoot issues:

```yaml
# appsettings.yml
logger:
  levels:
    slskd.Mesh.ServiceFabric.Services.PrivateGatewayMeshService: Debug
    slskd.Common.Security.DnsSecurityService: Debug
    slskd.Common.Security.LocalPortForwarder: Debug
```

#### Key Log Messages

- `AUDIT: Tunnel opened` - Successful tunnel creation
- `AUDIT: Tunnel rejected` - Tunnel creation failed with reason
- `AUDIT: Tunnel closed` - Tunnel termination
- `DNS resolution blocked` - DNS security violation
- `Rate limit exceeded` - Resource limit enforcement

#### Health Checks

Monitor VPN service health:

```bash
# Check active tunnels
curl http://localhost:5000/api/v0/port-forwarding/status

# Check pod membership
curl http://localhost:5000/api/v0/pods/{podId}

# Verify mesh connectivity
curl http://localhost:5000/api/v0/system/status
```

### Performance Tuning

#### Resource Limits

Adjust resource limits based on usage patterns:

```json
{
  "maxConcurrentTunnelsPerPeer": 10,
  "maxConcurrentTunnelsPod": 30,
  "maxNewTunnelsPerMinutePerPeer": 20,
  "idleTimeout": "02:00:00",
  "maxLifetime": "48:00:00"
}
```

#### Network Optimization

- Use IP addresses instead of hostnames when possible
- Implement connection pooling for frequently accessed services
- Configure appropriate timeouts for network conditions
- Monitor bandwidth usage and adjust limits accordingly

## Advanced Configuration

### High Availability

For production deployments, consider:

1. **Multiple Gateway Peers**: Designate backup gateway peers
2. **Load Balancing**: Distribute tunnel load across multiple gateways
3. **Redundancy**: Implement automatic failover for gateway failures
4. **Monitoring**: Set up comprehensive monitoring and alerting

### Integration Patterns

#### Service Discovery

Integrate with service discovery systems:

```json
{
  "registeredServices": [
    {
      "name": "Consul Service",
      "destinationHost": "consul.service.consul",
      "destinationPort": 8500,
      "protocol": "tcp"
    }
  ]
}
```

#### Identity Management

Leverage pod membership for access control:

- Use pod membership as primary authorization mechanism
- Implement role-based access controls through pod member roles
- Integrate with external identity providers for enhanced security

#### Network Segmentation

Implement network segmentation through pod policies:

- Separate pods for different security domains
- Use allowlisting to enforce network boundaries
- Implement least-privilege access patterns

## Best Practices

### Security

1. **Principle of Least Privilege**: Only allow access to required destinations
2. **Regular Audits**: Review tunnel logs and access patterns regularly
3. **Policy Updates**: Keep pod policies current with changing requirements
4. **Access Reviews**: Periodically review and validate pod membership

### Operations

1. **Monitoring**: Implement comprehensive monitoring of tunnel activity
2. **Resource Planning**: Monitor resource usage and plan capacity accordingly
3. **Backup Gateway**: Designate backup gateway peers for redundancy
4. **Documentation**: Maintain clear documentation of pod policies and procedures

### Performance

1. **Resource Limits**: Set appropriate limits based on expected usage
2. **Connection Pooling**: Reuse connections for frequently accessed services
3. **Caching**: Leverage DNS caching for improved performance
4. **Load Balancing**: Distribute load across multiple gateway peers

## API Reference

### Pod Management

- `POST /api/v0/pods` - Create new pod
- `PUT /api/v0/pods/{podId}` - Update pod configuration
- `POST /api/v0/pods/{podId}/members` - Join pod
- `DELETE /api/v0/pods/{podId}/members/{peerId}` - Leave pod

### Port Forwarding

- `POST /api/v0/port-forwarding/start` - Start port forwarding
- `POST /api/v0/port-forwarding/stop/{port}` - Stop port forwarding
- `GET /api/v0/port-forwarding/status` - Get forwarding status
- `GET /api/v0/port-forwarding/available-ports` - Get available ports

### Monitoring

- `GET /api/v0/system/logs` - Access system logs
- `GET /api/v0/system/metrics` - Access performance metrics
- `GET /api/v0/mesh/status` - Mesh network status

## Conclusion

The slskdN Pod-Scoped Private Service Network provides a secure, flexible solution for accessing private services within trusted pod communities. By combining strong security controls with comprehensive audit capabilities, it enables organizations to safely extend their private networks through encrypted mesh tunnels while maintaining strict access controls and resource governance.

For additional support or questions, consult the project documentation or community forums.


