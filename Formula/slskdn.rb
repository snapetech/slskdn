class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.145"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.145/slskdn-main-osx-arm64.zip"
      sha256 "908271497e66e07005d5eb64faa379803ac3871fc3c32e782179879adf2ef776"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.145/slskdn-main-osx-x64.zip"
      sha256 "c4953ed4bb32e8afd98aa5c3f0f7b656861c8b73f79166174c82d982807f12c8"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.145/slskdn-main-linux-glibc-x64.zip"
    sha256 "f5693b38bdb3958b802a1a97842350f5297b79a6c4c76f29118aef13a0c519af"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
