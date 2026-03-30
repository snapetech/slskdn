class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.113"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.113/slskdn-main-osx-arm64.zip"
      sha256 "1573313cfaa5df2642975e23cd388918948b13d573b0f0d02986cac5635af7a4"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.113/slskdn-main-osx-x64.zip"
      sha256 "f268896864ea7eb2bae1c28d14ffec5bd69bd308a8dd0c54d75f53a44babdd81"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.113/slskdn-main-linux-x64.zip"
    sha256 "c8785feb2ae9c6bcaa1ef0740a5cc437ea2317c6c9064da1aa6c1ae2e96996ca"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
