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
        version = "0.24.1-slskdn.32";
        
        sources = {
          "x86_64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-${version}-linux-x64.zip";
            sha256 = "97f8a416672c9721d6f20f95ab656621ae0804ff4f3be8654dc5f4814111ea97";
          };
          "aarch64-linux" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-${version}-linux-arm64.zip";
            sha256 = "e81d49779e57a340eb199b29f9a5ead4aabb4a18ed8fcbaa95104103405b520c";
          };
          "x86_64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-${version}-osx-x64.zip";
            sha256 = "b9df79881378f82ab0d06fa15c8a58d69e0b2ca59a0a7f03ef7e10d3a1f7f4b2";
          };
          "aarch64-darwin" = {
            url = "https://github.com/snapetech/slskdn/releases/download/${version}/slskdn-${version}-osx-arm64.zip";
            sha256 = "74ade24b5bedd45567fcdb11d10fe4eb91867ddfb78ef03539805614cf195466";
          };
        };
      in
      {
        packages.default = if builtins.hasAttr system sources then
          let
            srcConfig = sources.${system};
          in
          pkgs.stdenv.mkDerivation {
            pname = "slskdn";
            inherit version;

            src = pkgs.fetchurl {
              inherit (srcConfig) url sha256;
            };

            nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper ];

            unpackPhase = "unzip $src";

            installPhase = ''
              mkdir -p $out/libexec/slskdn $out/bin
              cp -r * $out/libexec/slskdn/
              chmod +x $out/libexec/slskdn/slskd
              
              makeWrapper $out/libexec/slskdn/slskd $out/bin/slskdn \
                --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [ pkgs.icu pkgs.openssl ]}
            '';
          }
        else
          throw "Unsupported system: ${system}";
      }
    );
}
