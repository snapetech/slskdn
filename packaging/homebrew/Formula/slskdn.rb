class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.156"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.156/slskdn-main-osx-arm64.zip"
      sha256 "611ca02b4b0af26f5674e9b9d68f0dbc08df470cffd0d2a6d9de3a3014a2bf43"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.156/slskdn-main-osx-x64.zip"
      sha256 "28735a711f3c17b45c43fef74744681de9bfd7967db0912721754ac5c74a25b6"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.156/slskdn-main-linux-glibc-x64.zip"
    sha256 "4eb51d12503c9a1ec50107f0baa4222d04ada06cfae976ada6692dbe2cbfc912"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
