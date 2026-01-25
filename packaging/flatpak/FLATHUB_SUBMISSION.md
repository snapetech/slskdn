# Flathub submission checklist

Before submitting to Flathub, update `io.github.slskd.slskdn.yml` as follows.

## 1. .NET 8.0 runtime

- Replace the `dotnet` module `sources[0].url` with the official .NET 8.0 runtime linux-x64 tarball from <https://dotnet.microsoft.com/download/dotnet/8.0> (e.g. `dotnet-runtime-8.0.x-linux-x64.tar.gz`).
- Set `sources[0].sha256` to the correct checksum (or use `type: file` and `url` with checksum from the download page).
- Alternatively, use `runtime/org.freedesktop.Sdk.Extension.dotnet8` if available in the Freedesktop runtime and adjust the `dotnet` module to install from there.

## 2. slskdn application

- Set `sources[0].url` for the `slskdn` module to a real release, e.g.  
  `https://github.com/snapetech/slskdn/releases/download/TAG/slskdn-TAG-linux-x64.zip`  
  (or `slskdn-dev-TAG-linux-x64.zip` for dev builds).
- Set `sources[0].sha256` (replace `PLACEHOLDER_SHA256_UPDATE_WITH_REAL_RELEASE`).
- Ensure `build-commands` copy the correct layout: the zip contains `slskd` (or `slskd.dll`), `wwwroot/`, and dependencies. The wrapper expects `slskd.dll` and `/app/dotnet/dotnet`; align paths if the publish layout differs.

## 3. slskdn.png

- The manifest references `slskdn.png` (512×512). Ensure it exists in `packaging/flatpak/` or the path in `sources` is correct.

## 4. Build and test

```bash
flatpak-builder --force-clean build-dir io.github.slskd.slskdn.yml
flatpak-builder --run build-dir io.github.slskd.slskdn.yml sh -c "echo OK"
flatpak build-bundle build-dir slskdn.flatpak io.github.slskd.slskdn
flatpak install --user slskdn.flatpak
flatpak run io.github.slskd.slskdn
```

## 5. Flathub

- See <https://docs.flathub.org/docs/for-app-authors/submission/>.
- App ID `io.github.slskd.slskdn` must match the Flathub application; ensure the `app-id` in the manifest is what Flathub expects.
