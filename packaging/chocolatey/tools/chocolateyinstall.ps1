$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.105/slskdn-main-win-x64.zip"
$checksum   = "4f912e16278738d52fac314f483f27a9f435143aba8f56defaa3d2546bd5736c"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
