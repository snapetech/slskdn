$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.187/slskdn-main-win-x64.zip"
$checksum   = "13167fb5dc4ec8ae0b38f5cd9ef03db15f4e6cccfbe75757c8f04e7af71bbf94"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
