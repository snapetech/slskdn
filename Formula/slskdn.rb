class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.104"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.104/slskdn-main-osx-arm64.zip"
      sha256 "7d86d31855fdc91f8e93b725964b189ea3ac588de87b386770ddd32355413512"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.104/slskdn-main-osx-x64.zip"
      sha256 "687ade2ca2fbe5d332aa4c6204ea8c1ee7888e6f3440282efc1afbcae8f86a5d"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.104/slskdn-main-linux-x64.zip"
    sha256 "83ffef7821cd20e41549f4581025e88e7f02bbd0cfa14e359cf1dd86e00236ff"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
