class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.123"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.123/slskdn-main-osx-arm64.zip"
      sha256 "d155cf011a5507fc021bd0a89a56d30e091bec6e95d95362de6bddfc8e25d951"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.123/slskdn-main-osx-x64.zip"
      sha256 "dd62eff0770224074c193091d46042b4a9c0656af78f32c862f83858f1573a59"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.123/slskdn-main-linux-x64.zip"
    sha256 "a049b3786a99a3bf6cf0efcfbe524361f7948744f413822fe5e1207f3c4c6de8"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
