class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.96"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.96/slskdn-main-osx-arm64.zip"
      sha256 "91e929f70d49c7b91c59539f1027825e2e34d2654ace53eea03a2e2167d30331"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.96/slskdn-main-osx-x64.zip"
      sha256 "9c593bb137a73e325cf63ca0f86e4882feaf61d158916ac52affd03256030d2f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.96/slskdn-main-linux-x64.zip"
    sha256 "d9174a64c785e29c094e2019a357a0a63cddf5730d5bf3fdc95b8139dae7db85"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
