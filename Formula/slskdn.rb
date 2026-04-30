class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026043000-slskdn.209"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.209/slskdn-main-osx-arm64.zip"
      sha256 "08ee49583f26550db21ba07dd13350040ef93691abfaf0e742e7f24b75882d3c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.209/slskdn-main-osx-x64.zip"
      sha256 "2508c7deab5250e71b0bce8a709dfed627c48c505982295ed3708ab8cbd662cd"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.209/slskdn-main-linux-glibc-x64.zip"
    sha256 "89111b8d29b14258a15eafb4518949497f928e8d57c36c5b369d5923463ebc7d"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
