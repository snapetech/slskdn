class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.143"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.143/slskdn-main-osx-arm64.zip"
      sha256 "db1589937fb9be13c79cf0e892e9ce371021e4f35786aa4b620fe0aaa1719ac3"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.143/slskdn-main-osx-x64.zip"
      sha256 "20f21d14ff204b1ca6e975a98b022b29b0f763a4d0cdb5f779c5fae63516096e"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.143/slskdn-main-linux-glibc-x64.zip"
    sha256 "b052e0da657b684af5c5e478f1894364392b9382ee16f0af7fbf5184fb5ca403"
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
