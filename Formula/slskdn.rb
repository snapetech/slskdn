class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.100"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.100/slskdn-main-osx-arm64.zip"
      sha256 "3d41766fa6a013ceddc650577082c2dafcf0fb309b0bfee0847de1c2c237a651"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.100/slskdn-main-osx-x64.zip"
      sha256 "47e1d46ec06db473557534b15e3d4b8296b5f253f7f6789a75de43f5063cd14b"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.100/slskdn-main-linux-x64.zip"
    sha256 "c2d7d00d873da63779dfb54169a6e4da198315915ae3adf6f51ee8a08aee2770"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
