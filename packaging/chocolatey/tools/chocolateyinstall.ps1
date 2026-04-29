$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.196/slskdn-main-win-x64.zip"
$checksum   = "357df800fe19fa82f4bb73607db53391be1363a43367b21e9be06a9c5ba4fd09"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
