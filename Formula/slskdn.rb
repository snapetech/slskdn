class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.114"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.114/slskdn-main-osx-arm64.zip"
      sha256 "cd5ba0a4f8a16d6d775c4339f9740b81254aa0e51534b38b08b502ebf9f5a035"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.114/slskdn-main-osx-x64.zip"
      sha256 "747de2c7a48f1d55761bf21015d4bde41f342933cf15269be5f6599c29eac4e0"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.114/slskdn-main-linux-x64.zip"
    sha256 "9706d0cdf1c4416fe177f2e1b694eeef9d103e4106aa118c9d628a3888818600"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
