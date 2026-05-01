# Windows And macOS VPN Enforcement

The Windows and macOS paths enforce fail-closed egress for the configured slskdN
process or service user. They do not manage VPN clients and should usually be
paired with static or provider-managed forwarded-port state.

## Windows

Windows uses Windows Defender Firewall rules to block the configured slskdN
program on non-VPN interfaces while leaving loopback and the named VPN interface
usable.

Required inputs:

- `SLSKDN_APP_PATH`: full path to the slskdN executable.
- `SLSKDN_VPN_IFACE`: interface alias shown by Windows for the active VPN.

Example:

```powershell
$env:SLSKDN_APP_PATH = "C:\Program Files\slskdN\slskd.exe"
$env:SLSKDN_VPN_IFACE = "ProtonVPN"
slskdN-vpn-agent platform-split
```

## macOS

macOS uses a `pf` anchor keyed by the service user and active VPN interface. Run
the command as root.

Required inputs:

- `SLSKDN_SERVICE_USER`: the user running slskdN.
- `SLSKDN_VPN_IFACE`: active VPN interface, usually `utunN`.

Example:

```bash
sudo SLSKDN_SERVICE_USER=slskd \
  SLSKDN_VPN_IFACE=utun4 \
  slskdN-vpn-agent platform-split
```

## Limitations

- The agent does not start or authenticate the VPN client on Windows or macOS.
- Dynamic NAT-PMP namespace claiming is a Linux/WireGuard path.
- Forwarded-port discovery should be static or provider-managed unless a
  provider-specific backend is added.
- The Web UI/API should remain local; Soulseek traffic is the traffic being
  constrained to the VPN path.
