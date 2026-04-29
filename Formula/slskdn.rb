class Slskdn < Formula
  desc "Unofficial slskd fork with batteries-included Soulseek features"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "2026042900-slskdn.196"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.196/slskdn-main-osx-arm64.zip"
      sha256 "abc0c85120f4a53dd934eaff724db1c082af0e0c0af06af32f0ab5cd679d159b"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.196/slskdn-main-osx-x64.zip"
      sha256 "75b1cd640f20d51d0a60cdc07241b7542e65ef038df697fc88e4fb8a6ea7ab44"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.196/slskdn-main-linux-glibc-x64.zip"
    sha256 "30cf6282d7aebdde917ee09bb89268090cc8351c90159a1e44dc7329fd006c16"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
