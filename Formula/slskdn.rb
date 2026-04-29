class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.198"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.198/slskdn-main-osx-arm64.zip"
      sha256 "742b35aaad5850ade4c74b34727182669242803d11668010beb9a316f9d667d3"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.198/slskdn-main-osx-x64.zip"
      sha256 "b662c24f60b8718410b5137abb8906ecc168aae34d74e53747b250ce39bff640"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.198/slskdn-main-linux-glibc-x64.zip"
    sha256 "3910a07ae06aca8d552639318942235ef1d046322fc4462d9c50a5d0a2f6e16c"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
