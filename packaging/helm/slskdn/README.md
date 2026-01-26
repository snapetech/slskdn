# slskdn Helm Chart (Generic Kubernetes)

Helm chart for [slskdN](https://github.com/snapetech/slskdn) on any Kubernetes cluster. No TrueCharts or TrueNAS-specific dependencies.

## Prerequisites

- Kubernetes 1.19+
- Helm 3.10+
- PV provisioner (if not using a default `storageClass`)

## Install

```bash
# Add repo (when published) or install from path
helm install slskdn ./packaging/helm/slskdn

# With a values file
helm install slskdn ./packaging/helm/slskdn -f my-values.yaml

# Override key options
helm install slskdn ./packaging/helm/slskdn \
  --set env.SLSKD_USERNAME=myuser \
  --set env.SLSKD_PASSWORD=mypass \
  --set image.repository=ghcr.io/snapetech/slskdn \
  --set image.tag=0.24.1-slskdn.40
```

## Main values

| Section | Key | Default | Description |
|--------|-----|---------|-------------|
| **image** | `repository` | `slskd/slskdn` | Image |
| | `tag` | (Chart `appVersion`) | Override tag |
| | `pullPolicy` | `IfNotPresent` | Pull policy |
| **service** | `main.port` | `5030` | HTTP port |
| | `https.enabled` | `false` | Expose HTTPS 5031 |
| **persistence** | `config.enabled` | `true` | PVC for `/app/config` |
| | `config.size` | `1Gi` | Size (and optional `storageClass`) |
| | `downloads` | `10Gi` | `/app/downloads` |
| | `shares` | `10Gi` | `/app/shared` |
| | `incomplete` | `5Gi` | `/app/incomplete` |
| **env** | `SLSKD_*` | (see `values.yaml`) | Soulseek, API, mesh, privacy, etc. |
| **ingress** | `enabled` | `false` | Create Ingress |
| | `hosts[].host` | `slskdn.local` | Host(s) and paths |
| | `tls` | `[]` | TLS entries |

## Required env (override in `env` or via `--set`)

- `SLSKD_USERNAME` – Soulseek username
- `SLSKD_PASSWORD` – Soulseek password

Use a Secret and `env` / `envFrom` in a custom values file for production.

## Upgrade / Uninstall

```bash
helm upgrade slskdn ./packaging/helm/slskdn -f my-values.yaml
helm uninstall slskdn
```

## Links

- [slskdN](https://github.com/snapetech/slskdn)
- [slskd configuration](https://github.com/slskd/slskd/wiki/Configuration)
