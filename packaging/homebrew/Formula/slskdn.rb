class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.97"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.97/slskdn-main-osx-arm64.zip"
      sha256 "db7f3e605f111fa86e6bd527b8f472103336d0fa2edcb2f41b1922a31626a408"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.97/slskdn-main-osx-x64.zip"
      sha256 "298ef4b88732f0ac8ccdcfc3f3258dff93b98c9364b00423fd4b9b5a7dd0a28d"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.97/slskdn-main-linux-x64.zip"
    sha256 "ada54ed76a8e32cdbf35cbed422a62eba079c4766677ec63d384554d36da241e"
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
