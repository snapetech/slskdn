# External Tunnel Setup

Use this path when OpenVPN, Tailscale, a provider desktop client, or another
service already owns the VPN tunnel interface. The slskdN VPN agent still
applies fail-closed routing, exposes forwarded-port state, and can reconcile
ingress, but it does not start the tunnel client.

## Supported Inputs

- `SLSKDN_VPN_TUNNEL_TYPE`: `openvpn`, `tailscale`, or another descriptive
  external tunnel type.
- `SLSKDN_VPN_IFACE`: the interface name, such as `tun0` or `tailscale0`.
- `SLSKDN_VPN_TUNNEL_SERVICE`: optional systemd unit used for health checks.
- `VPN_PORT_FORWARD_BACKEND`: usually `static` for external tunnels.
- `SLSKDN_VPN_STATIC_FORWARD_DIR`: directory containing provider forward files.

## OpenVPN Example

Start the provider-managed client first:

```bash
systemctl is-active openvpn-client@provider
ip link show tun0
```

Configure the agent service:

```ini
[Service]
Environment=SLSKDN_VPN_TUNNEL_TYPE=openvpn
Environment=SLSKDN_VPN_IFACE=tun0
Environment=SLSKDN_VPN_TUNNEL_SERVICE=openvpn-client@provider
Environment=VPN_PORT_FORWARD_BACKEND=static
Environment=SLSKDN_VPN_STATIC_FORWARD_DIR=/etc/slskdN-vpn/static-forwards
```

## Tailscale Example

Tailscale usually does not provide generic public VPN port forwarding. Use this
mode for private tailnet routing or custom exit-node setups where you provide a
static/manual forwarded-port state.

```ini
[Service]
Environment=SLSKDN_VPN_TUNNEL_TYPE=tailscale
Environment=SLSKDN_VPN_IFACE=tailscale0
Environment=SLSKDN_VPN_TUNNEL_SERVICE=tailscaled
Environment=VPN_PORT_FORWARD_BACKEND=static
Environment=SLSKDN_VPN_STATIC_FORWARD_DIR=/etc/slskdN-vpn/static-forwards
```

## Static Forward Files

Create one file per known provider mapping:

```bash
sudo install -d -m 700 /etc/slskdN-vpn/static-forwards
sudo tee /etc/slskdN-vpn/static-forwards/pf0.env >/dev/null <<'EOF'
public_port=51000
public_ip=203.0.113.10
local_port=50300
proto=tcp
EOF
```

`pf0` is the legacy Soulseek mapping consumed by older slskdN VPN integration
code. Additional `pfN.env` files can be exposed through
`/v1/slskdn/portforwards`.

## Apply Changes

```bash
sudo systemctl daemon-reload
sudo systemctl restart slskdN-vpn-split.service
sudo systemctl restart slskdN-vpn-gluetun-compat.service
sudo systemctl restart slskdN-vpn-ingress.service
sudo systemctl restart slskdN-vpn-ingress-renew.timer
sudo /usr/local/bin/slskdN-vpn-agent verify
```

If the provider does not support inbound port forwarding, outbound Soulseek
traffic can still be forced through the VPN, but inbound reachability will be
limited.
