class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.57"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.57/slskdn-main-osx-arm64.zip"
      sha256 "10ecb29eda4a8f69ae06b83aff6f8b7ab7aff8c19859616254434840caa6c0c9"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.57/slskdn-main-osx-x64.zip"
      sha256 "0727765782c1b07a97ad2e9435f423686027b7f60b984b427a19c1b047322e0a"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.57/slskdn-main-linux-x64.zip"
    sha256 "57d205e559665d5dc8e0af24a11159fa72401d6fa4ebb882a9a902e009d06733"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
