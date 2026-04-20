$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.166/slskdn-main-win-x64.zip"
$checksum   = "a904f70464605b693e0ae3e5a7088e4659c16be5b5805dfae48311b7d769f478"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
