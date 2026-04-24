class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.178"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.178/slskdn-main-osx-arm64.zip"
      sha256 "a13b94234fdc8ad064646d5aa30d937817c5daad1ae98dd4639463d98d2cd971"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.178/slskdn-main-osx-x64.zip"
      sha256 "ffc45f58b6b71765e2826cedbce8bb65ace367b81b742d14656eea20b0882673"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.178/slskdn-main-linux-glibc-x64.zip"
    sha256 "47814e69d76e0465363cf1792fe4f5e669ec5507f88422022104dbb39b39a4bb"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
