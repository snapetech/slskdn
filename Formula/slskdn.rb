class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.176"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.176/slskdn-main-osx-arm64.zip"
      sha256 "5b4a7a29b733983447c9cdd6e7eb7d51ef4f659b9bd40ad9d13fe7b0e03c0e70"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.176/slskdn-main-osx-x64.zip"
      sha256 "344d8ca4218a3c6ba8d5d1b61b8408139f03ae5a8fc520a8fa0ffe1dbe70989d"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.176/slskdn-main-linux-glibc-x64.zip"
    sha256 "f9dd2b3699e47981a44a7619819ddfec85b897d5e8b5a8983840ff5f9a94f85f"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
