class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.101"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.101/slskdn-main-osx-arm64.zip"
      sha256 "9fff181f3e760af3416ad71dd9220e44e1347d5b7690928a6c84063d716606b1"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.101/slskdn-main-osx-x64.zip"
      sha256 "10df20cebfd40bbcb967764106746adb07b52c5ea68500d9982714549d3dbf6f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.101/slskdn-main-linux-x64.zip"
    sha256 "dee4a4936d6e3066d4afea6735e9b21cf3ba6cf4b647090fc55b441dd6676443"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
