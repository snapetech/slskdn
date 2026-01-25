$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-win-x64.zip"
$checksum   = "5fac0d3fef7dd29fe404bf37c9149e446284ee86b480b953370c03b2f0ca1e5e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
