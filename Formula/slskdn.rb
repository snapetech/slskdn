class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.25.1-slskdn.2"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-osx-arm64.zip"
      sha256 "5d1ff5d9068550839f80d8b047da632d7a34f0950f61ebe635d11109be2fc413"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-osx-x64.zip"
      sha256 "4bad699622d136af4e238a1aeb08c2b79ed07f391728188e0e102de8aa99a1dc"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-linux-glibc-x64.zip"
    sha256 "b20f3dfb7ff750be2c76c42527232f94904523c5fbc417455023f5a88d076d5d"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
