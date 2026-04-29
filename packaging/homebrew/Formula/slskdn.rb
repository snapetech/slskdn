class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.188"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.188/slskdn-main-osx-arm64.zip"
      sha256 "0b0dc15f7008a8be2a70525ce236603e02393ed28695d4bb7fff78a326987132"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.188/slskdn-main-osx-x64.zip"
      sha256 "20e1d8ae4739df2e0cd9fca19ce7c68383ddd438ee450b45374f75c901bd1ec0"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.188/slskdn-main-linux-glibc-x64.zip"
    sha256 "55cf2672f2ab8c63cf376cdb65f190ed0f9afd601cb5e29be352a9580517eac2"
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
