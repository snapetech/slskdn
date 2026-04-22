class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.175"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.175/slskdn-main-osx-arm64.zip"
      sha256 "5d0876901c34f4c1914c157b139f56ebc8147f0bb6c51d5d18e3709495e11a87"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.175/slskdn-main-osx-x64.zip"
      sha256 "a29d4c0353a2aa3c669dcdb670fc6e49a68bee7b13601bf97b251e495f406dc7"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.175/slskdn-main-linux-glibc-x64.zip"
    sha256 "cae18fd577a6bce3e5ae012506461784f2d561fc10fef07a1a1a7dfc231f77dd"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
