$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.130/slskdn-main-win-x64.zip"
$checksum   = "7a69c3c0fa643900f7fc8284a6d8f0acc9d7975a0a7056e18d11308c4648543c"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
