class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.59"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.59/slskdn-main-osx-arm64.zip"
      sha256 "0ebbd86b03be6e53deb878bdcdf0bdca9c822dedd7f2b1a9a930ff3a190781d2"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.59/slskdn-main-osx-x64.zip"
      sha256 "92cbd5bcf43157e135c40ba6e2d65ad75f19a6ab7fd15e2e3cc12b2ecd931d49"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.59/slskdn-main-linux-x64.zip"
    sha256 "725e1cd2f5101d05ba5b086d749a7339d116689a890da8abdc6471a3d6f1fef9"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
