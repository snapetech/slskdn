class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.193"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.193/slskdn-main-osx-arm64.zip"
      sha256 "c8037d49f7a18d4877bd630e525406304897fcb52f00fd6a79c022f86604f314"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.193/slskdn-main-osx-x64.zip"
      sha256 "66b5cb8fd2021a9bd94aa84e0a4ebad0123101a66ed9dd4d43626fa5f7af25b7"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.193/slskdn-main-linux-glibc-x64.zip"
    sha256 "d52b39cbbd78f33519a6b50e4b71385073e23c44ed990ee88793ee0803f7ccd3"
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
