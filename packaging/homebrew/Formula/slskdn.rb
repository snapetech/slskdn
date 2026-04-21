class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.171"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.171/slskdn-main-osx-arm64.zip"
      sha256 "a6d3f6db63a8944a4a53c2cda7e20908683fec4c5b4b64571a002647fb65c03c"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.171/slskdn-main-osx-x64.zip"
      sha256 "14d6bec7d1a70a562b7e82c44b6d4c0487e6fa94043291c646efe41c0eefe9c3"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.171/slskdn-main-linux-glibc-x64.zip"
    sha256 "3e0d63c485c1ad55dd4d94c6fd4a8aab345c776ba3978769ccf8d42549de3843"
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
