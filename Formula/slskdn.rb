class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.149"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.149/slskdn-main-osx-arm64.zip"
      sha256 "be1a9ada872f348d96692efd6a471023aea4d4ec3e8893f7b78aafc171d4ce59"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.149/slskdn-main-osx-x64.zip"
      sha256 "be2da9615cff0840b5ecd85633393b1584bde973b893a01704cb37f67f5424bc"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.149/slskdn-main-linux-glibc-x64.zip"
    sha256 "c28dda728e20f4ca6c13deeb913eb0b7d3c6eda0858b70a6ca776e869cddcc97"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
