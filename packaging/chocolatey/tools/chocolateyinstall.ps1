$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.143/slskdn-main-win-x64.zip"
$checksum   = "184485050cc718d98a1e386777a928b18ce98b5cfc65806c4bc4e7ae9d264e29"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
