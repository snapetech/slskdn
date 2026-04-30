class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026043000-slskdn.205"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.205/slskdn-main-osx-arm64.zip"
      sha256 "d55670a4f5aae2c3091be4cbd7a69662baee6f7665e1847564de8bfc74e6145c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.205/slskdn-main-osx-x64.zip"
      sha256 "aef93ddd76c095f46a3e923ea458a8b4415f31bce603d1dc889a183db1946af8"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.205/slskdn-main-linux-glibc-x64.zip"
    sha256 "9a05451bf0d0fd0f1cfa1285c0a5cf8b76c5327128da5abad102432075243c06"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
