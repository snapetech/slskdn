class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "0.24.5-slskdn.109"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.109/slskdn-main-osx-arm64.zip"
      sha256 "86f5eae02cf8a663cfa4cc5f2e374000f740c03f78d07aadc85d417a044523ec"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.109/slskdn-main-osx-x64.zip"
      sha256 "97524cfe5688b308150a361b9cdbc87ca8b442ee6c65b8261860264bc7db73bd"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.109/slskdn-main-linux-x64.zip"
    sha256 "325fb8cf68f3babfc7c11000764b3a33da260f7c8e5ccc2ff6717e427de01589"
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
