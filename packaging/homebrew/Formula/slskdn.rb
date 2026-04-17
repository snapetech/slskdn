class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.135"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.135/slskdn-main-osx-arm64.zip"
      sha256 "2cb94a213250fff5c8c7fb32bc4cbe9e019d0ee826508f5889ec3ebad1f4abe6"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.135/slskdn-main-osx-x64.zip"
      sha256 "1bb1f7051025e505ef97d0229e242e5c6bafce73915aaf076292f58044d077da"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.135/slskdn-main-linux-glibc-x64.zip"
    sha256 "9139952767d362aed6ea2bc0ac0db116e355fea4be8aee727b669ce032333de3"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
