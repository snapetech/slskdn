$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.215/slskdn-main-win-x64.zip"
$checksum   = "9a949060a72ff8a17fca0fcfc0b53b82d7b9aea8a9c5d314e361ed2bc52f8284"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
