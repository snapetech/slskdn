class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.112"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.112/slskdn-main-osx-arm64.zip"
      sha256 "3de51e58a19d94efbba5238e4e17f3bd4e8d3507171f7051d010a0e946364779"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.112/slskdn-main-osx-x64.zip"
      sha256 "a0294251be87f17e203da220bd566bb20de5c456f3059e3adcbe7bb160dd434e"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.112/slskdn-main-linux-x64.zip"
    sha256 "ebad4346d60c264e773da8e1c722b0f1811607340c288ff4c1f94336747f7c8c"
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
