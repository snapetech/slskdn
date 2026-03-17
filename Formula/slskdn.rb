class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.55"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.55/slskdn-main-osx-arm64.zip"
      sha256 "b56925262b2f3066dc8b3a9ce622e059a322315193c879a1cdf1597b4631e927"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.55/slskdn-main-osx-x64.zip"
      sha256 "c7e854fbbec5c0049e0c750ece29280328fe268c7aa7e6bef2526383dc96ab27"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.55/slskdn-main-linux-x64.zip"
    sha256 "b83ed207607658c45a57d48ad5c0a5fab89f2ecfc6af29177768a88002e04c5a"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
