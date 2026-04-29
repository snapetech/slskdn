$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.191/slskdn-main-win-x64.zip"
$checksum   = "e1c049b357b658d0f8896fd664827faedbe4a7374e35be1c7c8d311e0a6b8bc8"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
