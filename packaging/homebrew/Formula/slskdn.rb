class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.164"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.164/slskdn-main-osx-arm64.zip"
      sha256 "392e325a1d1e83b6776d29dee09a811974b932ab83e4c4b432206a4beaf93a73"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.164/slskdn-main-osx-x64.zip"
      sha256 "ff507c02f69631db925af2fe873079afa99f9170ad150a4f0f5560b313af9ab9"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.164/slskdn-main-linux-glibc-x64.zip"
    sha256 "a1f383455b5b7f2cb299494bd97f19e12c01b517518abf58bd513b14184ceaae"
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
