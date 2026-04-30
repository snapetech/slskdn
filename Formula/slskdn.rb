class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026043000-slskdn.208"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.208/slskdn-main-osx-arm64.zip"
      sha256 "4f6a4b77583cd293621fb0989d13bc8ee138d27851079bf51314e17e385932ad"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.208/slskdn-main-osx-x64.zip"
      sha256 "e86fbb854c6e1f30b0d87205a41bcc4733d4dedbd84f630abf95cea40d1a9ed2"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.208/slskdn-main-linux-glibc-x64.zip"
    sha256 "e0c13ad3d886f9748d9babb5627691db8ddcd34c0276293d7a7814b67afa0d5e"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
