class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.95"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.95/slskdn-main-osx-arm64.zip"
      sha256 "5607cd3467e7e55d41bc14b81634c8119d9cf0bb071cab21f64c973be1e381ec"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.95/slskdn-main-osx-x64.zip"
      sha256 "b7e17b8ca4e3a5975c4d8ee856b1eccafc0fe967f5592504a37339fb8e1dd248"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.95/slskdn-main-linux-x64.zip"
    sha256 "0ac1e3bd592067e15e6f415659156ea9a33fda7ed205713e7d5f36d32c86653c"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
