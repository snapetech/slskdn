class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.169"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.169/slskdn-main-osx-arm64.zip"
      sha256 "20ea857a65bdbdf7bee425e3a86367116c2d72a225489aabaf4031d50233aab5"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.169/slskdn-main-osx-x64.zip"
      sha256 "10337e5426e40541f8b875ebaf118b5aaceeb235ffd0558093e1755c6b9801f0"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.169/slskdn-main-linux-glibc-x64.zip"
    sha256 "98611723b5ddacc5ccacb68d989c1107603a101f4c3041691a4051a4f9bf0ff1"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
