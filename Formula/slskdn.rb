class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.136"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.136/slskdn-main-osx-arm64.zip"
      sha256 "ecd8a2abf57b74332b488765014d47d1d87fe6f795076f0e3a67f9b11cdba39e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.136/slskdn-main-osx-x64.zip"
      sha256 "2eac55cc44e674f063c1566bd8bb62dc9cc6d3a7e0bee39b63e5ddc4f20a1296"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.136/slskdn-main-linux-glibc-x64.zip"
    sha256 "159187c81d75d047db5d1d91a2ad6ecf99de261bfcfec4e510d34fc7ed0d07bc"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
