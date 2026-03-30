class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.110"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.110/slskdn-main-osx-arm64.zip"
      sha256 "79ce99ae2256320eb6262700a6d43a25fff726b37e732f65059808484ab4a167"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.110/slskdn-main-osx-x64.zip"
      sha256 "9ff0dc7deff1079869dc407a194c4142efaef9c8b1e6f06a771311ffb6da3b49"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.110/slskdn-main-linux-x64.zip"
    sha256 "2c4ca45ec078b625cdae5a483a431d0780f721537063ac753e8612700db19c66"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
