class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.214"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.214/slskdn-main-osx-arm64.zip"
      sha256 "24556b7564504d2eb2ec95c24f07a0b92518aba0462622f152154d013d48158f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.214/slskdn-main-osx-x64.zip"
      sha256 "de20ce86ab2fafbeb276ea24c2a837bf815eb91e26c1d6b025e19531daafa1db"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.214/slskdn-main-linux-glibc-x64.zip"
    sha256 "1973f056f86f0eb173cea86057c90bb1cdd14ac3573745664e88deaff626adae"
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
