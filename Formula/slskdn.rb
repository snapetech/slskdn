class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.58"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.58/slskdn-main-osx-arm64.zip"
      sha256 "f055d9647abcae9587a47397b7c4b257904df0f34918b71dd11ae6b406d36a6f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.58/slskdn-main-osx-x64.zip"
      sha256 "bafd740f3f5401f50e01068f358c0b592e06c616518dcb948919792fc4daa032"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.58/slskdn-main-linux-x64.zip"
    sha256 "279ba3af54db2e97972352a5238750d9960f1ea420f8be2f81f920fc9271d62e"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
