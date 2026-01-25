class Slskdn < Formula
  desc "Decentralized mesh community service for Soulseek with VPN and advanced networking"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.1-slskdn.40"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-osx-arm64.zip"
      sha256 "889a506b62c33a6127c5bf198c7509b7fe7618c4a993d18ee20381cf395466d6"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-osx-x64.zip"
      sha256 "1d3cdf1623c43852244951689c0a574f6e53b78b2d53b86575cb9fb1d041a56e"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-linux-x64.zip"
    sha256 "72f06e4505cc1b8ec4e2b43602cb4bd768b89bc2b9d372de70c920aab42271b7"
  end

  def install
    # Install all files to libexec
    libexec.install Dir["*"]
    
    # Create a shim in bin that points to the binary in libexec
    # We rename the command to 'slskdn' to match the package name
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskdn --help", 1)
  end
end
