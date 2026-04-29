class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.191"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.191/slskdn-main-osx-arm64.zip"
      sha256 "93af5848283a28848cb7ac2a0a950e487485b35a64efc48e09ea435f4b3c48a2"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.191/slskdn-main-osx-x64.zip"
      sha256 "151dea8600f34c5e5969edd25dff3d1391a0807930001436a72732a70fa30658"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.191/slskdn-main-linux-glibc-x64.zip"
    sha256 "2d7732b96fa7cd9ed0ea5b3babfa3cada9043979c8b26fab5043e2764f2208e4"
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
