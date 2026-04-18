class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.138"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.138/slskdn-main-osx-arm64.zip"
      sha256 "a08cb2b57b1a5ece8fa45f30dccf9cffbc77186a45893fd363576e3257bd4d66"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.138/slskdn-main-osx-x64.zip"
      sha256 "b7c17330d2191168f30471dd35696a3703db770537e3c31f2ec5bbe2e9ec0a11"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.138/slskdn-main-linux-glibc-x64.zip"
    sha256 "6ac74a93f67d7fea33de27f531488753f1bfedc07205cb34b9d1d41f62f6ffec"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
