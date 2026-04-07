$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.117/slskdn-main-win-x64.zip"
$checksum   = "e644c0207d3d03671439ec5f2f6c0b1486f411178daaf9392e73cad54615e8b2"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
