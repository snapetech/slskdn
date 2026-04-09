$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.120/slskdn-main-win-x64.zip"
$checksum   = "483e61ddc9896b2509b23e9370273fb35410b970e6c729fca94bb869e2e1b3ad"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
