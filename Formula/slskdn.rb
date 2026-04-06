class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.116"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.116/slskdn-main-osx-arm64.zip"
      sha256 "35cea0874bb485123bd0713a79a70125d0bdcdc4cab7956c217f01f84d0fd45c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.116/slskdn-main-osx-x64.zip"
      sha256 "1b659c1bc904bea89f7b3974ae43fca4469c8518e2bb8bc4bedb40469d09eeb2"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.116/slskdn-main-linux-x64.zip"
    sha256 "49fcabf73c6aed8d1bc36d7a9777c84f412ba8584ab78426587301a19b3273a1"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
