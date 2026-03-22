class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.91"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.91/slskdn-main-osx-arm64.zip"
      sha256 "6603148926ddb36dedd5446e49bd77e8207ec21225412447eeb88a297d9767a1"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.91/slskdn-main-osx-x64.zip"
      sha256 "ebbe0e1ea66f31b6e436a123075655980ff296155204b896ee1d52f8dca85160"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.91/slskdn-main-linux-x64.zip"
    sha256 "a39241caf9345f9432688771c940b7cc0893d5044704df5ba9e8f3ceaf794623"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
