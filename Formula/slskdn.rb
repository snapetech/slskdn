class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.76"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.76/slskdn-main-osx-arm64.zip"
      sha256 "c1cf3b0c4d1fd5d8cab5e36743915a48e0c4c2d3a448716a9251374137eeba80"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.76/slskdn-main-osx-x64.zip"
      sha256 "67fa6ad646efc73d56d9924573ca37c9ec051a63dfaef8cb11fd3b7666e7e2d0"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.76/slskdn-main-linux-x64.zip"
    sha256 "199c992edcd90cf49e4f121103bb3d37e78d66438eb0780d26145ee295aa2bef"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
