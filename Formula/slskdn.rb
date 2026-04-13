class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.126"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.126/slskdn-main-osx-arm64.zip"
      sha256 "8aa9928ab480226067fecbb861eb377ef20b228e1aa7d1d4af04f3638a88e658"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.126/slskdn-main-osx-x64.zip"
      sha256 "9dbefd7196924f4c6cfe8f0a10199dfaf55ca6be95c0145e4957a6ecfb1dd76f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.126/slskdn-main-linux-x64.zip"
    sha256 "ff0ed8708c522e8309fee4274299c7ce51e7801ac6c70f74d55055529bb7f5f9"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
