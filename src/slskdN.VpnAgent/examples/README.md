# Example Configs

These files show the shape of the external configuration this bundle expects.
They are intentionally redacted. Do not commit real WireGuard private keys or
provider credentials.

Copy real provider configs into:

- `/etc/wireguard/slskdN-vpn.conf`
- `/etc/wireguard/slskdN-vpn-ingress/*.conf`

For static forwarded-port providers, copy static mapping files into:

- `/etc/slskdN-vpn/static-forwards/pfN.env`
