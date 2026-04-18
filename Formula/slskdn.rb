class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.141"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.141/slskdn-main-osx-arm64.zip"
      sha256 "1d3603c5434c9d600547020c3b6ad643faeabf15fb8346bc7251e022cb3eafe4"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.141/slskdn-main-osx-x64.zip"
      sha256 "ebc83121935a8f6f90be4e3500228d6f5864cb82f0353974087bd0473b54588b"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.141/slskdn-main-linux-glibc-x64.zip"
    sha256 "9849ac6ea21e997c3a0fe8bd4508f7579ecad196b715339d4fd02f44526a1168"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
