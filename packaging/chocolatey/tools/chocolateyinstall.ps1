$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.218/slskdn-main-win-x64.zip"
$checksum   = "c9e9fd846a0e0258d4d098cb55b7e950c719c1669add35c50457ee299ecaab45"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
