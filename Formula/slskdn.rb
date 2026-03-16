class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.54"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.54/slskdn-main-osx-arm64.zip"
      sha256 "5300ed2f96bf4ab0a6abb9ca691df7c2599bfed65095c6b1bffc52e5d4a457b0"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.54/slskdn-main-osx-x64.zip"
      sha256 "ff2d8bf6819060c84eedfae0a725a22945d19e19bd44caa3a0a3a6edef5338f1"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.54/slskdn-main-linux-x64.zip"
    sha256 "335814c955edd623d48e387d78585e0c1456bff922c40808c65bc844c6dc928a"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
