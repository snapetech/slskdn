class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.118"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.118/slskdn-main-osx-arm64.zip"
      sha256 "87e6b367e5e76dca65957784871cab0012a00f54c7975287b6bfcbf61de929be"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.118/slskdn-main-osx-x64.zip"
      sha256 "466b4ddb105df455b9083bc997ffe250d71ad25ab25f23de213b158bb4a0f366"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.118/slskdn-main-linux-x64.zip"
    sha256 "10adfb9fce16d4aaf895f460e138da5b76c2f37e16c8cc6b4f28088e512e9ced"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
