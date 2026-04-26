class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.181"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.181/slskdn-main-osx-arm64.zip"
      sha256 "b5d7b9823497a5e300b940012d24ebdb67c13c9a666cd580303e08edfbb1f3b4"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.181/slskdn-main-osx-x64.zip"
      sha256 "e39e8cfd30f3d0869342af06b75ad6f7fe4f4e7e2c7a273aad9fbd030cb13319"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.181/slskdn-main-linux-glibc-x64.zip"
    sha256 "caf103ffa63191b1fca4eb34cebfb5ed1a65b58915c1c32333b191b537679e34"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
