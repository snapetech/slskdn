class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.90"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.90/slskdn-main-osx-arm64.zip"
      sha256 "4083070f654ca6968d2385df4b21eb487b5d3df91ed7cd86c8e9e041316e22e8"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.90/slskdn-main-osx-x64.zip"
      sha256 "55b10e6fd9f564d11d10ba484e5597a6c900555aee4f1eaf7f55c41131ab2e66"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.90/slskdn-main-linux-x64.zip"
    sha256 "5f202b5f8c8c7c81c6de7b2eb0ae67ff1fb1d7f8fea1ae74103c261d5cbc1cc1"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
