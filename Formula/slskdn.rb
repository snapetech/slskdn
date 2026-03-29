class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.103"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.103/slskdn-main-osx-arm64.zip"
      sha256 "17a3ff2328b3f20c7973158b30eed76ff8058b962c0644f07f304da446283010"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.103/slskdn-main-osx-x64.zip"
      sha256 "f63c3ce005f6b9ab7b9c144f5921145896b1ff4a7f862d6b482eb533240e1380"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.103/slskdn-main-linux-x64.zip"
    sha256 "1e3eaf4fd0339c586640a62674a193a1dacd7258dfd3f34e8abc755997a36fb8"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
