$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.138/slskdn-main-win-x64.zip"
$checksum   = "c9d2733ab4c812176e90fdef0600fbf9cc3362a8aae1ab3ff03c427da22f85a7"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
