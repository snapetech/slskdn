class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.129"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.129/slskdn-main-osx-arm64.zip"
      sha256 "c3d676648088d12d46271330ba04ae37a47eb885c6ed84c5060a2f0615038afb"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.129/slskdn-main-osx-x64.zip"
      sha256 "c38c198b251c377b4cf8a6ad8bef39901bdbcb39bf4f9cf5bff29b0d2f3da845"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.129/slskdn-main-linux-x64.zip"
    sha256 "57ea2ee5e3d81fbeebd7a0f8c54dc56cce0f93ff09b8c8f5d992b20a30ecd92a"
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
