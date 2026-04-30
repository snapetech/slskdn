class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.202"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.202/slskdn-main-osx-arm64.zip"
      sha256 "2309a40c891c01a557a7bee77fb76daf004c47e75b3d5169d26a46ca82c5a4f5"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.202/slskdn-main-osx-x64.zip"
      sha256 "4d351c28607ea51f2fc96b05a426f9c9008aa612d62a45386c1be1105f01167f"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.202/slskdn-main-linux-glibc-x64.zip"
    sha256 "c899179eed5894ab3afc8f80fbf5a70224584eda7c2062a2ea62770a0050216b"
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
