$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.118/slskdn-main-win-x64.zip"
$checksum   = "26d2239088678053c424b27b51ecedf6dacaf55051b2652777e8044d80f46f37"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
