class Slskdn < Formula
  desc "Decentralized mesh community service for Soulseek with VPN and advanced networking"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.52"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-osx-arm64.zip"
      sha256 "e42eced926326913d51c36a32fa113bf89897cb24e0b3a596864e0473e887eb9"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-osx-x64.zip"
      sha256 "2acc71004c6af271a789c5127681684108876fe8a49b04396b4cf5c98a0afbc0"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-linux-x64.zip"
    sha256 "be92597f23c00f3d6114353a2c253c6ec8d99b5cad0113ddc7bd5ea2433d48fe"
  end

  def install
    # Install all files to libexec
    libexec.install Dir["*"]
    
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
