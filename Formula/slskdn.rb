class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.172"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.172/slskdn-main-osx-arm64.zip"
      sha256 "1b113b87630f31264778b2338b3f853ac572994fd912a611d60a3f4e226b1bb0"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.172/slskdn-main-osx-x64.zip"
      sha256 "1d29ff1bcd68664dbf77ae7258521e189694b0333287b3bb697014949500de4e"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.172/slskdn-main-linux-glibc-x64.zip"
    sha256 "c1971cadae2d5741d95a31010f2a1f800e23b9f1d8862e9f70de2a2b5daa9df1"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
