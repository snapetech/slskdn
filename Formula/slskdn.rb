class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.78"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.78/slskdn-main-osx-arm64.zip"
      sha256 "ff6d0101b7b00ab02551eb95b53d668000f0878662c4f3654b8e5fe885f6dcda"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.78/slskdn-main-osx-x64.zip"
      sha256 "e8170fbff5e7eaee330902a9b086140c2e6dc5791f6000d267147a22496adb06"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.78/slskdn-main-linux-x64.zip"
    sha256 "171ffd7e50dd1c28e6286ae523b5d4eaa383fad26a2484bb11b3d38bcbe0dad5"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
