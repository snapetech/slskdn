class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.124"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.124/slskdn-main-osx-arm64.zip"
      sha256 "956b557dd4680bc74a53af90a23a8390502204b1ef2d72e283e432b9c5cdb428"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.124/slskdn-main-osx-x64.zip"
      sha256 "a168c91f3131f1f0886fbb0b5e047cf1bed6620015cdd0971339c14bd09b8097"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.124/slskdn-main-linux-x64.zip"
    sha256 "be953cd9ded9f9588b9df85f004ea62150321d4a904c995179b1c71c21588a67"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
