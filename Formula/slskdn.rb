class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.120"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.120/slskdn-main-osx-arm64.zip"
      sha256 "45d0b3797f2e3cdf418c3a47f55f0355a7d1428afcc35ae9cc6eeba5fdca9888"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.120/slskdn-main-osx-x64.zip"
      sha256 "c76b13d516a29c0ec8746c38c0689cddc753808f256fabcb98d25245ae6c1163"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.120/slskdn-main-linux-x64.zip"
    sha256 "8750db80636fcd8de47a0fe6aca06c6ccb2fa7d0b04231d4a68b25508d711db5"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
