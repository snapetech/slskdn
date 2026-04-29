$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.194/slskdn-main-win-x64.zip"
$checksum   = "0a73bcd45e7b4cb7bf6f8757be33ef4fe7ae9bc0dff3af1f41e8d104a2bda75e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
