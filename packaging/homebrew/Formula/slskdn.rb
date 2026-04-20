class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.165"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.165/slskdn-main-osx-arm64.zip"
      sha256 "f3a0d129448d413a3a19cc5c868e77e175e82c41075b1a61c45d1603197c7d58"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.165/slskdn-main-osx-x64.zip"
      sha256 "7a8ed99b6a0f6942fe34f56f0b98753685a3ca9056f38e180e58251dc2ccf000"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.165/slskdn-main-linux-glibc-x64.zip"
    sha256 "ec55296e2f026ed88e43977b3fb4e4879824650d26de590bd3dfb6ed6abfb422"
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
