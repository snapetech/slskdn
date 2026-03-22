{
  description = "Batteries-included Soulseek web client";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
        version = "0.24.5-slskdn.89";
        devVersion = "0.24.1.dev.91769727133";
        devTag = "build-dev-${devVersion}";
        
        # Helper function to build slskdn from a given version and sources
        mkSlskdn = { pname, version, sources }:
          if builtins.hasAttr system sources then
            let
              srcConfig = sources.${system};
            in
            pkgs.stdenv.mkDerivation {
              inherit pname version;

              src = pkgs.fetchurl {
                inherit (srcConfig) url sha256;
              };

              nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
              dontStrip = true;
              buildInputs = [
                pkgs.curl
                pkgs.icu
                pkgs.krb5
                pkgs.lttng-ust.out
                pkgs.libunwind
                pkgs.openssl
                pkgs.stdenv.cc.cc
                pkgs.util-linux
                pkgs.zlib
              ];

              unpackPhase = "unzip $src";

              installPhase = ''
                mkdir -p $out/libexec/${pname} $out/bin
                cp -r * $out/libexec/${pname}/
                chmod +x $out/libexec/${pname}/slskd

                # .NET's trace provider still references the old SONAME on current nixpkgs.
                patchelf \
                  --replace-needed liblttng-ust.so.0 liblttng-ust.so.1 \
                  $out/libexec/${pname}/libcoreclrtraceptprovider.so
                
                makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd \
                  --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [
                    pkgs.curl
                    pkgs.icu
                    pkgs.krb5
                    pkgs.lttng-ust.out
                    pkgs.libunwind
                    pkgs.openssl
                    pkgs.stdenv.cc.cc
                    pkgs.util-linux
                    pkgs.zlib
                  ]}
                ln -s slskd $out/bin/${pname}
              '';

              meta = with pkgs.lib; {
                description = "Batteries-included Soulseek web client" + 
                  (if pname == "slskdn-dev" then " (Development Build)" else "");
                homepage = "https://github.com/snapetech/slskdn";
                license = licenses.agpl3Plus;
                platforms = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
              };
            }
          else
            throw "Unsupported system: ${system}";
        
        stableSources = {
          "x86_64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-main-linux-x64.zip";
            sha256 = "sha256-g5QlG2klIc34FB3wKmBiYBxhZtkT2Oz1zz4/C9f6VSg="; # x86_64-linux
          };
          "aarch64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-main-linux-arm64.zip";
            sha256 = "sha256-INmHHRK6HV62k/ZNzw7pATnFBmjA5a8Ex+nq15Uyhu8="; # aarch64-linux
          };
          "x86_64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-main-osx-x64.zip";
            sha256 = "sha256-ksvVvPQxV+E1xAum4tZa118Zpqt/0V4uPMErLs2THUk="; # x86_64-darwin
          };
          "aarch64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-main-osx-arm64.zip";
            sha256 = "sha256-DrvYawO+blPeuHi9zfC9ypyCLe3X8rGpqTD/OhkHgdI="; # aarch64-darwin
          };
        };

        devSources = {
          "x86_64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${devTag}/slskdn-dev-linux-x64.zip";
            sha256 = "1bz25gy9p0h3jin4zfhp5msvy8aqxbniq4m50q2xikp6p7bhw1km"; # x86_64-linux
          };
          "aarch64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${devTag}/slskdn-dev-linux-arm64.zip";
            sha256 = "0000000000000000000000000000000000000000000000000000000000000000"; # aarch64-linux
          };
          "x86_64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${devTag}/slskdn-dev-osx-x64.zip";
            sha256 = "0000000000000000000000000000000000000000000000000000000000000000"; # x86_64-darwin
          };
          "aarch64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${devTag}/slskdn-dev-osx-arm64.zip";
            sha256 = "0000000000000000000000000000000000000000000000000000000000000000"; # aarch64-darwin
          };
        };
      in
      {
        packages = {
          default = mkSlskdn {
            pname = "slskdn";
            version = version;
            sources = stableSources;
          };

          slskdn-dev = throw "slskdn-dev flake output is temporarily unavailable because no matching build-dev release is currently published. Use a stable package or publish a build-dev release first.";
        };
      }
    );
}
