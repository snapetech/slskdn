class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.174"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.174/slskdn-main-osx-arm64.zip"
      sha256 "cb55b3b2a04d5025ed7b615a9798c43ede7df8d1a1b87d0fb9a159f7fb9cc7b4"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.174/slskdn-main-osx-x64.zip"
      sha256 "0bdf339f17e63e8121e220a75cc32752ddd3b4d35f2da7a06807bc12291aad23"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.174/slskdn-main-linux-glibc-x64.zip"
    sha256 "a11b773cf7c51d51478f19a0ed846b6d4ecad200eba677c1c8f7d0512f089190"
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
