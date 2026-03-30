class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.111"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.111/slskdn-main-osx-arm64.zip"
      sha256 "8e9c8b549eba1af19c2a6db122d1248558503dbdbeb33de7b50ccbc19e4848c2"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.111/slskdn-main-osx-x64.zip"
      sha256 "fe6f33a0a81a5735bfd4946a6b350d98968d87f3aebd7a57633acc5b796d834b"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.111/slskdn-main-linux-x64.zip"
    sha256 "5fd1c926f80f04802966d64a7bfb170547a86a84d88ae061af9666676dfa85cc"
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
