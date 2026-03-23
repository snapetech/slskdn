$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.90/slskdn-main-win-x64.zip"
$checksum   = "2857d42adc11512939cf7022d5e357e55728ca9da9ca7bfc64d835133859a869"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
