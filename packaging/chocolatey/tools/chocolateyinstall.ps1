$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-win-x64.zip"
$checksum   = "a83c059b4a56423cacd9ea7b466005f86ce3c5f00ec1002b1c029b686fcae9e6"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
