$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.110/slskdn-main-win-x64.zip"
$checksum   = "8ecb132cf6dc3d1017d1400e2570fbe0afdc7f75628717c47b23498526c3ecd8"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
