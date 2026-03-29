$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.97/slskdn-main-win-x64.zip"
$checksum   = "5a3ec95d5d4fc3ea78cf707a4d561f94afa28e95cd50d03405ca8101428e79aa"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
