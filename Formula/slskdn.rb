class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.25.1-slskdn.185"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-osx-arm64.zip"
      sha256 "072e77e66f2b4a281dc8c851c7b6295d29e27e022e0d75092333f4c5d9f1c0fc"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-osx-x64.zip"
      sha256 "618c393d3cea914457ab643466aff8721425fa171a310be7f7afff41bc0e1a96"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-linux-glibc-x64.zip"
    sha256 "e16b829325fb6c14b3f098be057783bbea7c82527aa178ed71888712fcfc8a07"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
