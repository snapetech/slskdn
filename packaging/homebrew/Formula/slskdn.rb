class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.148"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.148/slskdn-main-osx-arm64.zip"
      sha256 "64c1b8b0b2ae00e08defa3c37dfcf96341a45453f83b34a93efbf14bbacc2b14"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.148/slskdn-main-osx-x64.zip"
      sha256 "eb50a9077725c6875ff092e657381e55a0e2cbc4d4db04eef4e291c6dab9fcb8"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.148/slskdn-main-linux-glibc-x64.zip"
    sha256 "6651048ea87d98ff8913540f0e76d05ed3f2b7b124d82fec27e355eb5f7fb582"
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
