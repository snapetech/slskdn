class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.144"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.144/slskdn-main-osx-arm64.zip"
      sha256 "2b39459522bccee0452b5d9c9436d645f891e46784700c445c66d21c95bb32b9"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.144/slskdn-main-osx-x64.zip"
      sha256 "c7b3946cf767cd3145bfe6b116c4b963e8075b6004bc203e81e763766862dd55"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.144/slskdn-main-linux-glibc-x64.zip"
    sha256 "735203f59d46075e2f3c7071c01bc6538e225202f90d5333ef8ff931c306a7eb"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
