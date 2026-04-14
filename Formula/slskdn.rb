class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.128"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.128/slskdn-main-osx-arm64.zip"
      sha256 "d850e737c98b1d66d44c94a845ec3daa0753394b1e09d13a869f57b16ead54d8"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.128/slskdn-main-osx-x64.zip"
      sha256 "baabf248ea42662286521e4693a1447325ea458ee2b7734dc27e5f44f8577a83"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.128/slskdn-main-linux-x64.zip"
    sha256 "ebc6ea07c382f49416e45f4835f456ba48c05604465214cbc5942436a0da05b6"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
