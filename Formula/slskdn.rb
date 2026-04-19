class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.154"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.154/slskdn-main-osx-arm64.zip"
      sha256 "05af36c5ae60b3a0407335698a0821a3fa83b0159aa4dca3db681ca21e01c760"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.154/slskdn-main-osx-x64.zip"
      sha256 "679e0d2e9630bd7e7ec65c56b306aa3ebb2a0f02a7d6a6d1144f24675f86642e"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.154/slskdn-main-linux-glibc-x64.zip"
    sha256 "03fac16bb29db9fd1b0ead075e5f90594a337980d11f8619126b2c9a1de6d556"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
