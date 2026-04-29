class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.192"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.192/slskdn-main-osx-arm64.zip"
      sha256 "cb5a509aa3f2457b4c3c2bde176f100487249ee810e9102fd5ccbd8233546d5e"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.192/slskdn-main-osx-x64.zip"
      sha256 "2fe6377f68a5a5502379d191e78851ab163396306eec90b63811e50678fb8c79"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.192/slskdn-main-linux-glibc-x64.zip"
    sha256 "7ed8293205bec3465d4b2126b45bf09e3e929c93fa62a8a39b2f0c1a798af69f"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
