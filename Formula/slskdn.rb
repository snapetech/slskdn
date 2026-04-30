class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.204"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.204/slskdn-main-osx-arm64.zip"
      sha256 "0aaa054b80faa7fe66326e9853237b462db757713c7775e7a89111836c148af1"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.204/slskdn-main-osx-x64.zip"
      sha256 "3350f9873f84fe3c111eb65d3dbc43962399281a7cadf73ce042bb06aebc7604"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.204/slskdn-main-linux-glibc-x64.zip"
    sha256 "d43a48c7a320448e526094978e70fd6ae9d6ca53b92f24eac40b416592de8137"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
