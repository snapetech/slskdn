class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.105"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.105/slskdn-main-osx-arm64.zip"
      sha256 "8fdb455db8b01331117bb9f2313bb2dda97ec7d58fb08ae8123460260d18c2ce"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.105/slskdn-main-osx-x64.zip"
      sha256 "23c4c1f2f387095162ab6d324896332ea24e90d7a3a5d40503c6c03307053534"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.105/slskdn-main-linux-x64.zip"
    sha256 "8a49771ffc856f79f7c47ad63cf2759d3a200e356cfba94ad81416621fb7bf0d"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
