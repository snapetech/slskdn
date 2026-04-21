$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.172/slskdn-main-win-x64.zip"
$checksum   = "abf3717847a007ad1773213b9fe3656bde7d787de279e5e52e4907516444d737"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
