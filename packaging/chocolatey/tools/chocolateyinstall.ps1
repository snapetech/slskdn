$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.171/slskdn-main-win-x64.zip"
$checksum   = "fcd9f338e23fbe16b2d0e3bcbf2bc217c634ebf0cb8e68e051dcdc440baf7d47"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
