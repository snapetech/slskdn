class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.217"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.217/slskdn-main-osx-arm64.zip"
      sha256 "f46489e1d1b0f3b77523cbd5b836368c5efab859d454185fbc7cc281cd74c408"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.217/slskdn-main-osx-x64.zip"
      sha256 "9aabb16db14a5d5bf702226f01058e01558cdf33923c4191eeaee03836368478"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.217/slskdn-main-linux-glibc-x64.zip"
    sha256 "b77722b5ec0f00ea4a87ffe847443fe334e83fabe155a22e05c5a928daf0172f"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
