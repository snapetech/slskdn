$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.165/slskdn-main-win-x64.zip"
$checksum   = "d4c0543b8cfb830bd5f10c037d9b51f25d4b4332b9b2cae087afbfc26441ef4e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
