$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-win-x64.zip"
$checksum   = "8b8067ea49f6c0173896db2946887778b01eb0cb738add0e4a6d9be6dd62f46a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
