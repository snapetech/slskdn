class Slskdn < Formula
  desc "Decentralized mesh community service for Soulseek with VPN and advanced networking"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.48"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.48/slskdn-main-osx-arm64.zip"
      sha256 "e6d70615d26212c54d3e3e7f154989307d957b781b386322c0eb35db65bf7222"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.48/slskdn-main-osx-x64.zip"
      sha256 "2fbed80fb8e191b9018a42c1eb317c598315bc6fa01cbb235803d43364b2a174"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.48/slskdn-main-linux-x64.zip"
    sha256 "c4e827cc3c7495224083e54aa0ad6809991850946c025618cec018157c31d1ff"
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
