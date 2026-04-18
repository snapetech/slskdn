class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.139"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.139/slskdn-main-osx-arm64.zip"
      sha256 "7feb30b0cfd4e45d826fe75cc83ed99c89ed9812ad5cb80b13175886e474f14a"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.139/slskdn-main-osx-x64.zip"
      sha256 "f21113496cf15bdd681a01f5e7aabc8fe3ba6cddd346eb3391f1e7fd07e77e4c"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.139/slskdn-main-linux-glibc-x64.zip"
    sha256 "80dc61e564c5ea30268264f97986d81497048518f77c87ea0a51e9361aa33904"
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
