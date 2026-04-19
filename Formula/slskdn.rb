class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.159"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.159/slskdn-main-osx-arm64.zip"
      sha256 "05f06dbba547c2d57d896ee8c8f91a7fb06feed1a603cf7d00a99bb1d7f0579f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.159/slskdn-main-osx-x64.zip"
      sha256 "d5a99d2ab30d616ef988ce98dfef37950c42387b90e91372f2c6b4be4e38227f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.159/slskdn-main-linux-glibc-x64.zip"
    sha256 "e93f9659d0bdda0b93a1c99e0f9744fcdc9a7949fb199f2b32af086842fbb3df"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
