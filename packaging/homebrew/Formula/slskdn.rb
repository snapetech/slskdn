class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.151"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.151/slskdn-main-osx-arm64.zip"
      sha256 "627d5c1c11a8ba0b60aae3426ed0d80564c477ebe0ad4fec7ce16bf9cd7f022e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.151/slskdn-main-osx-x64.zip"
      sha256 "06c7154ae62e4a73aa6e3eff0c6ba7bd76136e1fe4ed1d72161d0d2fc8c56993"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.151/slskdn-main-linux-glibc-x64.zip"
    sha256 "d558609dee21e3ca498d1d33c8cf13c9f5bf916a9a172d543fcaee961b9ad41d"
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
