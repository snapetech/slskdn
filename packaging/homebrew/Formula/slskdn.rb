class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.190"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.190/slskdn-main-osx-arm64.zip"
      sha256 "c80dd3a2d69fb0358d7ebdad70fef687f4ef10987f274f56fb8df527c010313e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.190/slskdn-main-osx-x64.zip"
      sha256 "73108d5b5f82295f6fd8e60aa301ec6e366263f146ed87be46611c53f9535395"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.190/slskdn-main-linux-glibc-x64.zip"
    sha256 "61fba73faab156aa6b9782eb21cc56fe76fe5078c5e242afa6e6ad9f1dffd1cd"
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
