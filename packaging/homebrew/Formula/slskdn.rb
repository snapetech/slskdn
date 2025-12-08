class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.1-slskdn.32"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.32/slskdn-0.24.1-slskdn.32-osx-arm64.zip"
      sha256 "74ade24b5bedd45567fcdb11d10fe4eb91867ddfb78ef03539805614cf195466"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.32/slskdn-0.24.1-slskdn.32-osx-x64.zip"
      sha256 "b9df79881378f82ab0d06fa15c8a58d69e0b2ca59a0a7f03ef7e10d3a1f7f4b2"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.32/slskdn-0.24.1-slskdn.32-linux-x64.zip"
    sha256 "97f8a416672c9721d6f20f95ab656621ae0804ff4f3be8654dc5f4814111ea97"
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
