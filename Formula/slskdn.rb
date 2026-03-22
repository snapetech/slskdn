class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.89"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.89/slskdn-main-osx-arm64.zip"
      sha256 "3607f26fef243cbe11ad956c53b6f7a9bf32325ca9208b9aae0b23d01922cb13"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.89/slskdn-main-osx-x64.zip"
      sha256 "dec37ded5e3f6b09fe4430dd89a1b88a39ca7f0cfa2652f3fdf566f030320118"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.89/slskdn-main-linux-x64.zip"
    sha256 "8394251b692521cdf8141df02a6062601c6166d913d8ecf5cf3e3f0bd7fa5528"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
