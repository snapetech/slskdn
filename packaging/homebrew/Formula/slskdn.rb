class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.194"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.194/slskdn-main-osx-arm64.zip"
      sha256 "574588bf0358fc8e4a2486b68a1489898fbe85e94cef4a1df158180279e56af4"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.194/slskdn-main-osx-x64.zip"
      sha256 "b2fe3ade6d9b9b81438a8c25708207b120c4dc60e8edca1fe9f040603a595287"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.194/slskdn-main-linux-glibc-x64.zip"
    sha256 "88b892a03c2b86b7c1c91a12f74324f28c144285df43c5121417459d62290476"
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
