$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.217/slskdn-main-win-x64.zip"
$checksum   = "c21838daf5e482e04c8d3b4bfc8cf0c4fbfcc70c8faceca775907270399b0801"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
