$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.192/slskdn-main-win-x64.zip"
$checksum   = "ce92b830562f943da4524bbca3ad02a2b6f3a7e5ca88e7cb493fd569eb695c8f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
