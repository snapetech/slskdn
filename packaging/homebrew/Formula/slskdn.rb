class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.25.1-slskdn.185"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-osx-arm64.zip"
      sha256 "fdd931b32694feeec4a98b411885684fdc538c30b141727b443c9ed3f60c94a6"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-osx-x64.zip"
      sha256 "0870410b485a54525241eb9c52b25eee9e93e5542cf1ffdc2581a479706dfc97"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-linux-glibc-x64.zip"
    sha256 "8a93dbda85b0fb00acf9ea76efb297d45fc31ead9584fdc36114e885ead6bb2c"
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
