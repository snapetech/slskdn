class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.177"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.177/slskdn-main-osx-arm64.zip"
      sha256 "915df5fb787966641d1562b0a57a16a814285766d135f30022fbd97920c7eb6c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.177/slskdn-main-osx-x64.zip"
      sha256 "2ef944849d24c4be8968b583fb8c7987a7a2888355d6d6062fcbec3abcdcf0bd"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.177/slskdn-main-linux-glibc-x64.zip"
    sha256 "af668ae56c0447a56b20c45d965ddc7ed87fdd20a91861fcd6ecbfb4d45cd11b"
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
