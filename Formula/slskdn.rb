class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.137"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.137/slskdn-main-osx-arm64.zip"
      sha256 "08c4f19c0b351f3bf4e9d535dab0eac9ff29765f8b5abe7fab4cdc24fb4a1e70"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.137/slskdn-main-osx-x64.zip"
      sha256 "11e837e7de5f6864a69ee21a999c60cd8cf65be0f57982abee9fd121f542eb2f"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.137/slskdn-main-linux-glibc-x64.zip"
    sha256 "90fe5b39435650fe9ed149bfe8cad52a2b0945cafbe48da093e51ceb3ca33773"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
