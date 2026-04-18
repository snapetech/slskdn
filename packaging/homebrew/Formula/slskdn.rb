class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.146"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.146/slskdn-main-osx-arm64.zip"
      sha256 "533499f11c5a4af9788b3342ccfdb5a87f5101f6ee819e0e21dc106375035907"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.146/slskdn-main-osx-x64.zip"
      sha256 "e1c13d3b67f08eec8afb1e64823132c35626084bdcb28c2399b004fa2660d38d"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.146/slskdn-main-linux-glibc-x64.zip"
    sha256 "f46e163183fe111ced5f2b3c2ab063f0ecdb386d655f831a5e350525dbf5b9b7"
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
