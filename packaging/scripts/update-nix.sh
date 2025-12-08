#!/bin/bash
set -e
TAG=$1
LINUX_X64_SHA=$2
LINUX_ARM64_SHA=$3
OSX_X64_SHA=$4
OSX_ARM64_SHA=$5

cat > flake.nix <<FLAKE
{
  description = "Batteries-included Soulseek web client";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = nixpkgs.legacyPackages.\${system};
        version = "${TAG}";
        
        sources = {
          "x86_64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/\${version}/slskdn-\${version}-linux-x64.zip";
            sha256 = "${LINUX_X64_SHA}";
          };
          "aarch64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/\${version}/slskdn-\${version}-linux-arm64.zip";
            sha256 = "${LINUX_ARM64_SHA}";
          };
          "x86_64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/\${version}/slskdn-\${version}-osx-x64.zip";
            sha256 = "${OSX_X64_SHA}";
          };
          "aarch64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/\${version}/slskdn-\${version}-osx-arm64.zip";
            sha256 = "${OSX_ARM64_SHA}";
          };
        };
      in
      {
        packages.default = if builtins.hasAttr system sources then
          let
            srcConfig = sources.\${system};
          in
          pkgs.stdenv.mkDerivation {
            pname = "slskdn";
            inherit version;

            src = pkgs.fetchurl {
              inherit (srcConfig) url sha256;
            };

            nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper ];

            unpackPhase = "unzip \$src";

            installPhase = ''
              mkdir -p \$out/libexec/slskdn \$out/bin
              cp -r * \$out/libexec/slskdn/
              chmod +x \$out/libexec/slskdn/slskd
              
              makeWrapper \$out/libexec/slskdn/slskd \$out/bin/slskdn \\
                --prefix LD_LIBRARY_PATH : \${pkgs.lib.makeLibraryPath [ pkgs.icu pkgs.openssl ]}
            '';
          }
        else
          throw "Unsupported system: \${system}";
      }
    );
}
FLAKE
