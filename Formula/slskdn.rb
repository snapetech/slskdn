class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.187"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.187/slskdn-main-osx-arm64.zip"
      sha256 "dc494d46daa8c1ecd35845b31641d9c285b33c9bc2a09bffedfeea2eb1ca694f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.187/slskdn-main-osx-x64.zip"
      sha256 "a2dfc8a12975e0c6f381ac27ed1966c1e35665a0895e202c99252186d8abb4b1"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.187/slskdn-main-linux-glibc-x64.zip"
    sha256 "122e62cd5c1e89996e0abb50bae92fec73387173492813d936e34ca558ed2d20"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
