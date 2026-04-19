class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.157"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.157/slskdn-main-osx-arm64.zip"
      sha256 "6d84a71f905ca94c2bf1d76d19e675610ee236db604442864bb2fa70442e9940"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.157/slskdn-main-osx-x64.zip"
      sha256 "fff14d394c7d68147b307906908c390a82a0c2891ca6d8f7d3987bea0812ce1e"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.157/slskdn-main-linux-glibc-x64.zip"
    sha256 "c0c454aee6674f1654dcc0806a52e401aed5f7f8bc3217b9d156401e9e5cb0a1"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
