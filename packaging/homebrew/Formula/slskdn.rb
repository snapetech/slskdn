class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.162"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.162/slskdn-main-osx-arm64.zip"
      sha256 "c8775367dd59d71e9097844406ddbb91956d77e0dc11498f8ebf3b79525e5fce"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.162/slskdn-main-osx-x64.zip"
      sha256 "1ca2b0b1530413f8df1a79e04e9988f383a9590afa19e9c9665354b3f36b313f"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.162/slskdn-main-linux-glibc-x64.zip"
    sha256 "ab962142671f6eae8baafc4ed96ba534324dbd66c14d585deb29e15716ce9c0e"
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
