class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.215"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.215/slskdn-main-osx-arm64.zip"
      sha256 "27df0ad583abd213fd64f67777217506806ac36ed9d080b2c5ade35dd6320a36"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.215/slskdn-main-osx-x64.zip"
      sha256 "760a425811a8a4c518e3a1076e923acd5fcc52e18687601c38975399c7efe15b"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.215/slskdn-main-linux-glibc-x64.zip"
    sha256 "a5579b01df039d0dfdf1e4e4004fc1fdaebba93df262a4905d2e34900ad5c3de"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
