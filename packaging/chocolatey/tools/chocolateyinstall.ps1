$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.114/slskdn-main-win-x64.zip"
$checksum   = "81c769319a8ff0b32bb550ad487901e57c61c07f47bb5ef66d8e2959e31e20f9"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
