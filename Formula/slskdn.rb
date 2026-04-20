class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.166"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.166/slskdn-main-osx-arm64.zip"
      sha256 "c513cd9c3a9a225fb99288eb3f3535ef1eba8af2588b404e51600ccca400b8d5"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.166/slskdn-main-osx-x64.zip"
      sha256 "8c8956378acb75000b3fc2ae33494d6076a090d724975a038964ec246301cfbf"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.166/slskdn-main-linux-glibc-x64.zip"
    sha256 "60611755e3a958298e53d25952e5e6e1e0ec11c098632d5ed7b5fcaf671a0037"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
