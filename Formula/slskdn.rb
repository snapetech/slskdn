class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.140"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.140/slskdn-main-osx-arm64.zip"
      sha256 "98eff70456c82a78b90bfd59cb986e3f24f509ffa80b4c1af3f90805e94bd58c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.140/slskdn-main-osx-x64.zip"
      sha256 "288e35c298011d29b22e8d7cadd298c7db8d825364953ef6b47f5ea1565361de"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.140/slskdn-main-linux-glibc-x64.zip"
    sha256 "eccd219b7858e4e2f97ac9c6e3178c3c047dd4bb124e74aea83c02b73801f61d"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
