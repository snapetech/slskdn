class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.180"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.180/slskdn-main-osx-arm64.zip"
      sha256 "4b15eef69653770b5c81248f3c1b59a5bea251c7ca7cd0cd4cdbb15326eefa2c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.180/slskdn-main-osx-x64.zip"
      sha256 "e26b79817dee2e81757dd25af338d603b50d89c7e2d9f2a664cf7d282f64922d"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.180/slskdn-main-linux-glibc-x64.zip"
    sha256 "4856f39549bc7bc1f05c1ae1c018415b16ec5a211702b13db512013645466dbb"
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
