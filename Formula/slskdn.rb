class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.130"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.130/slskdn-main-osx-arm64.zip"
      sha256 "232442ab7fe1bad4b25d258641a80e32bce1d1fecedadbb306e1543ead4c084f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.130/slskdn-main-osx-x64.zip"
      sha256 "fc3b14b9356adf32dd8e2c57f76ce4fe15eb2aa821cde3eb340c8a324351875c"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.130/slskdn-main-linux-x64.zip"
    sha256 "9211b72defc2c8b615346725c63590a6fe650bb7cdd2469966ffea79d892a1ee"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
