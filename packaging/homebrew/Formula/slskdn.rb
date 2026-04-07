class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.117"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.117/slskdn-main-osx-arm64.zip"
      sha256 "cf02d135b488eb7244d7fc37e32bcd9d6cc3e4e8a7d004132513a45afc7a9951"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.117/slskdn-main-osx-x64.zip"
      sha256 "2ec4e271b89fc1ae68c290c815d7e88df9a8552e420dcdbe47e7270447bcc9ee"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.117/slskdn-main-linux-x64.zip"
    sha256 "c00c4f46a0172d21a45f77eb49b02790f0adf3142c2ec1fb061823cef3ab074b"
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
