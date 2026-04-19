class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.153"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.153/slskdn-main-osx-arm64.zip"
      sha256 "9194f32ebf3d5dd9ca42c9b109d57c5e0d10dd501f29dc3933851448de263de0"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.153/slskdn-main-osx-x64.zip"
      sha256 "d1e18cf4a65dc4fc95ee7058ebb443d809b620167e5a1c99f3189d8b5aa8b259"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.153/slskdn-main-linux-glibc-x64.zip"
    sha256 "d36c751026a613a86fefd7c51318dc95fe274ef9899040623bb84b0446796723"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
