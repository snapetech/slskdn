class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.197"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.197/slskdn-main-osx-arm64.zip"
      sha256 "88eafc5984afde21709792af79c82443a3b8ecedc5736152c98a13d01abe4d64"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.197/slskdn-main-osx-x64.zip"
      sha256 "12b385d4529225af3b608d317635bc00c7828000558775fc01e5a114e0a1072e"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.197/slskdn-main-linux-glibc-x64.zip"
    sha256 "af1b70bbd70655cfde49483be970437efc43cdd774ee41b7e75e9fe88aba9cee"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
