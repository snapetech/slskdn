$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.125/slskdn-main-win-x64.zip"
$checksum   = "30f56a39d6ae14ef7ec352b64e225350561bf53f9a292f0381acd431a5f6893e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
