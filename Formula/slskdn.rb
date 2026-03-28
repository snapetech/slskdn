class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.102"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.102/slskdn-main-osx-arm64.zip"
      sha256 "da9daa356ba02ef9235d8ed9bb060fd91b051ae8005b2af17f25a25c2aa365cf"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.102/slskdn-main-osx-x64.zip"
      sha256 "7989b5aeb67f9e7b74c60c4ec2e55a75961b578942051ae54f5e9b4646f1b2d8"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.102/slskdn-main-linux-x64.zip"
    sha256 "a9b56218671e1cdcb065448cc41f79b17c5e6a8b7131dfdb1160de3b52a3aeb9"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
