class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.218"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.218/slskdn-main-osx-arm64.zip"
      sha256 "45b403d95555d7a4fd220bbd5c0d88c0e98150dc3adbb9b3a3c25b86a94f9b24"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.218/slskdn-main-osx-x64.zip"
      sha256 "fcbf7a3688ff9c756bba9c010cf938357c8ac323ee2f4f0c95a0dd86c5e992dc"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.218/slskdn-main-linux-glibc-x64.zip"
    sha256 "b4781692df4f8ed2212c9335ebb5c7a65f27320cae6da01019cb4c278f65a28f"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
