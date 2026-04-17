class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.131"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.131/slskdn-main-osx-arm64.zip"
      sha256 "2252ca0ffbe152ba458d0923a012a79943728beb68e01b52db74f0eda0c62bba"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.131/slskdn-main-osx-x64.zip"
      sha256 "c86edc5fdef8d80f797bc79774c4c992c5a0a7ab1c35fd84949c21fa52a37e88"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.131/slskdn-main-linux-x64.zip"
    sha256 "96f423b7d9f07ca124a6f3f16743efa112607390c43f5e1687817ae0652d1007"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
