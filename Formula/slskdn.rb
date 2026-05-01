class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026050100-slskdn.213"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.213/slskdn-main-osx-arm64.zip"
      sha256 "11c09ee0fd5f9773f7ce75e103d77ae5d3c95b26ab38f650c3384e4980924c5e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.213/slskdn-main-osx-x64.zip"
      sha256 "f9b33c1fd110d5eb68516d93b17b3afee4d22e0b3d0f7d46e99286b45b3b8335"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.213/slskdn-main-linux-glibc-x64.zip"
    sha256 "e0f2511d489a2526a66cbb6cea14bb1787fa71897eef8f00b625ffc278b4a4b0"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
