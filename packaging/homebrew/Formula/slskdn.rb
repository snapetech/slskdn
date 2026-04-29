class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.186"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.186/slskdn-main-osx-arm64.zip"
      sha256 "05bbd57617fbd86d802d59d3a7e6c124c2611e77720106ba6a12c3512c2f53cb"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.186/slskdn-main-osx-x64.zip"
      sha256 "6d21804671d07476e3f9c5c127d7826b948c38369da2721fc7d393e88f20b405"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.186/slskdn-main-linux-glibc-x64.zip"
    sha256 "30064712ba2d2b0ae0f98578adecf3dc5c709ce499437a65d1e4c9089d2c5a66"
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
