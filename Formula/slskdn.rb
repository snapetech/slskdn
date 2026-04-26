class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.25.1-slskdn.2"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-osx-arm64.zip"
      sha256 "8966b83fc2c44e509b9d9e8ee7b1eb44be6cbfb580f1d91bd8ce00d043c14c3b"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-osx-x64.zip"
      sha256 "12f589b5872aa8cf11ac80cc17684f2f3a6d8cb539afe54db01d61820f9f5df9"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-linux-glibc-x64.zip"
    sha256 "c96de3fe95469e34f6e19e8f5038bf9c27b159db6da1e9d366cd6046f38e28ea"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
