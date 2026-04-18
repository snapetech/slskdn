$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.146/slskdn-main-win-x64.zip"
$checksum   = "f4669df6f2e24471966041d8ebf6371a2cade14d56ad1265228319cd72701e00"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
