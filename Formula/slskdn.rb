class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.142"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.142/slskdn-main-osx-arm64.zip"
      sha256 "530c26f99ea12b89f1b7fd22239c837d7fb5d5301accadcdb6151d9266f0ff83"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.142/slskdn-main-osx-x64.zip"
      sha256 "c38938ef11a71a8e30a690543ade09a5c0159ae467d38535626ac091af3106c0"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.142/slskdn-main-linux-glibc-x64.zip"
    sha256 "7735dfdeff16e7b8818980a32c8b0cf827dc19cf31c72b3014cceba8e7173264"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
