$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.168/slskdn-main-win-x64.zip"
$checksum   = "db0ad44270de6acec316ba89c3c55f79517c90e68d01bc607f53728d2b36559b"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
