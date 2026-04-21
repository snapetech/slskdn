class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.168"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.168/slskdn-main-osx-arm64.zip"
      sha256 "1fd9a9b5982b0e0d1ca9099f1e4f78e8fae8c9c406361a4339b803579a0fba4f"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.168/slskdn-main-osx-x64.zip"
      sha256 "d23eaadbc9a84d86980ab85a0a40787f437dd5563649d8182011ddd116ceb2e8"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.168/slskdn-main-linux-glibc-x64.zip"
    sha256 "8dacacc82e732e47d9e2b5f04532bd3bae135190c605932ac707884eff181630"
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
