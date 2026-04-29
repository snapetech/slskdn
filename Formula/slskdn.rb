class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.195"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.195/slskdn-main-osx-arm64.zip"
      sha256 "1e83b173a66a3915bddd9e0fb35e71b5e163529fb6ac4d132401b324d5f59d44"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.195/slskdn-main-osx-x64.zip"
      sha256 "1f4f45be8791141d9df9708c9e040ef7b56d24e4fef23f525dcdbaee9cb457e1"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.195/slskdn-main-linux-glibc-x64.zip"
    sha256 "c27e6b03a0cf8d7f9290f74abec908eb913a5c00feb93c22505780a04f949582"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
