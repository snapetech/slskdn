class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.60"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.60/slskdn-main-osx-arm64.zip"
      sha256 "2ef42dc9bae04de402279fd6f864907c3512ca8a03f17f48aab183d85e5b402e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.60/slskdn-main-osx-x64.zip"
      sha256 "01d6d2f03d9e37efcb8c60c710ece445799b245b88be8c84c8eea649ce7a7fdd"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.60/slskdn-main-linux-x64.zip"
    sha256 "b8c047ed1ff66bb0dfb14dfb2762106b84cb6d76afa5732d185c142871cbbc3f"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
