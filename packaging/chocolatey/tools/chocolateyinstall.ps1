$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.181/slskdn-main-win-x64.zip"
$checksum   = "9cee6cadd4ed7497b26b31b124966d347ad78308d905be12ad6e7db4cba503b1"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
