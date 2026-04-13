class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.125"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.125/slskdn-main-osx-arm64.zip"
      sha256 "c1f47ec2a085f05030e1fe001f0d4217993db10fd9316567df02b73d71cfb921"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.125/slskdn-main-osx-x64.zip"
      sha256 "85c50d89b2732581b25371bdd1b141b6e3241f5f583d9c69697cb1181625b377"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.125/slskdn-main-linux-x64.zip"
    sha256 "55b24a1d29756393719f494f6a80a9b219b747263528f26abc348286d5a70fd7"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
