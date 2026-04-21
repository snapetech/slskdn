class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.170"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.170/slskdn-main-osx-arm64.zip"
      sha256 "6eca153f33c408148ba32302b25c1452060cc5b19aafe5fcfbffa6fcd7719273"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.170/slskdn-main-osx-x64.zip"
      sha256 "e0d630be74ef51701907f991c71c9dd51d3eb63643fe9608719362de1d4cd9a1"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.170/slskdn-main-linux-glibc-x64.zip"
    sha256 "011d3e4f2e54f2e2d5b3912afecce94182418f9cb65542ef3b54329a82b49513"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
