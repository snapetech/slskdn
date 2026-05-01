$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.213/slskdn-main-win-x64.zip"
$checksum   = "ca0040cf3077884e26124636511ec7c7e41a06079f4a0e1d5eae5855cd9c71ac"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
