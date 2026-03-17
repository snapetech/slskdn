class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.56"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.56/slskdn-main-osx-arm64.zip"
      sha256 "e4d188d303b585878f5d26e73c50ee6e3be40edf616312077907c47ff7f743d3"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.56/slskdn-main-osx-x64.zip"
      sha256 "12b50a2a79efd3aa997694564f60638792ceb5b356a7cded79c44878eb974dc1"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.56/slskdn-main-linux-x64.zip"
    sha256 "7f6c1bb9d93b15c076095b72b777df29aca367b3814a0ca16fd5c72075d1ada5"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
