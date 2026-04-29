$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.198/slskdn-main-win-x64.zip"
$checksum   = "a61326bbec5d8469607e37b1c99a68d4b92e2590825363a7a158cefdf5a7e41a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
