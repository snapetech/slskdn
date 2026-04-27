class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.25.1-slskdn.184"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.184/slskdn-main-osx-arm64.zip"
      sha256 "7bf1ed7884d9ca937abccfe2b2fcdb51ac088399e81b1f2d1133f132803c946e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.184/slskdn-main-osx-x64.zip"
      sha256 "a2eb83a6e703f77b2a266d6ae01d210c01fa862a332392cb183ba585f32f680f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.184/slskdn-main-linux-glibc-x64.zip"
    sha256 "9431e6627965b4ec6366b940b71c82d097f437c6829c532ef8ea425a8d002e82"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
