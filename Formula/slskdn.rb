class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.77"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.77/slskdn-main-osx-arm64.zip"
      sha256 "42ba0f51e0c7baa59b4212b8c9cd687353c7a66d8acffba6e0a340edcfb11140"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.77/slskdn-main-osx-x64.zip"
      sha256 "394404d979cb06efbcfcc1960b5d617c8250419956b702bff7bb249df971483a"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.77/slskdn-main-linux-x64.zip"
    sha256 "210798bcc795abe9817bedaecb3927e6478bbdf951a3d0d92b0ac02751b62068"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
