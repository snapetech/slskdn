class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.189"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.189/slskdn-main-osx-arm64.zip"
      sha256 "3863488fdccf7298c5754afc7d3732d0221fc65183baf3f5b6e0f3d95ef42453"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.189/slskdn-main-osx-x64.zip"
      sha256 "3657e95dddebc44fc8f9abfc9e7b3603e63eba1e75a6827b07d0ca3e786c9d82"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.189/slskdn-main-linux-glibc-x64.zip"
    sha256 "95639f75abddfef73be3066d061fc40ea811e076f7bc3571b2992e3691d8467b"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
