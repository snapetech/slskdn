class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.152"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.152/slskdn-main-osx-arm64.zip"
      sha256 "7dfcbbbc82b2f46845459d8eedc380987c14226799703ab06dc90e893e71bb0b"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.152/slskdn-main-osx-x64.zip"
      sha256 "3fd56c5a74734ad8af0f7804939295d381b31e5f5d7e8bfb040b9a283a3f0064"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.152/slskdn-main-linux-glibc-x64.zip"
    sha256 "a15e51e98ea4ca53dbd732d28dc83a70dec70b53a23234160a9e9cb512232081"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
