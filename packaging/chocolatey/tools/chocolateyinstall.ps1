$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.112/slskdn-main-win-x64.zip"
$checksum   = "fb44b2c7467f8fa88a45a921dc5ec4e2d7335fde68e87de6b845d3a15cf6ee59"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
