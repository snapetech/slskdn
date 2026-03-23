class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.93"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.93/slskdn-main-osx-arm64.zip"
      sha256 "f254b9c8386ccd2e9206b60b5b8903fc5489ed4299fef66f64eccdadeb7c8e36"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.93/slskdn-main-osx-x64.zip"
      sha256 "d1a135d70f55fa3db249964b25821c80a259aa533a7f3b9c720a06e81087ee27"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.93/slskdn-main-linux-x64.zip"
    sha256 "6980b4305c2891402b6f20205c1fbef4aab21cb6eda97b3c116fceee19c9459d"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
