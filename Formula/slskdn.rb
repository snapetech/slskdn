class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.167"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.167/slskdn-main-osx-arm64.zip"
      sha256 "3d7a30709744b3fc43cc4deefd6ffd3860e8d99f036ba51d6eb1914666e6e612"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.167/slskdn-main-osx-x64.zip"
      sha256 "60cad7afcab9e206eb64039d5d05c83f03ffac13d1f8d37890fc00f063974d7e"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.167/slskdn-main-linux-glibc-x64.zip"
    sha256 "d98ec40ba6bb5a014f05d3777a421d7b75511a02d2a443b2fdc1657fd00180c1"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
