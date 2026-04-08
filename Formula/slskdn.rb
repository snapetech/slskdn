class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.119"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.119/slskdn-main-osx-arm64.zip"
      sha256 "ebf985728c5836da6a40b088d581a276a53b6163899a1cbe9db2814834b86854"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.119/slskdn-main-osx-x64.zip"
      sha256 "948bad0fe23a9464b38ee66b5cf46e360f8395d5883f6df357b0d47b0e976d63"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.119/slskdn-main-linux-x64.zip"
    sha256 "e5037af7bb555c56f3113fb600d9cc9bd98f311d40dee411cbe103ed3f45673e"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
