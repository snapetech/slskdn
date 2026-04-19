class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.155"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.155/slskdn-main-osx-arm64.zip"
      sha256 "44b74acb39e07c82167a18c5030e86ff63f5dbd8e2b322b7b4aafdcf6bf2d446"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.155/slskdn-main-osx-x64.zip"
      sha256 "493adcc8c0e2c5c0670d1e91af8e5a2e94f3bce196cefce7531769e26d1f9097"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.155/slskdn-main-linux-glibc-x64.zip"
    sha256 "6c61c43223c917770afe290ce9265497225ba6c329560c83b2f12d1b78ba0b43"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
