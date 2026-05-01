class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.216"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.216/slskdn-main-osx-arm64.zip"
      sha256 "9606cd9805998c3f69e6a668a597e831b8080fe5e179c3b546b837c09b286a6e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.216/slskdn-main-osx-x64.zip"
      sha256 "6e77d409ac05f12e6fa40f2523d05f58cd1a5ab761fa10437bb51b9ef5718fe3"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.216/slskdn-main-linux-glibc-x64.zip"
    sha256 "1755c51f2cdb03ba6a075c70ffbef449332a6dc2a9a4bff7b7b49ae4ca66ec7e"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
