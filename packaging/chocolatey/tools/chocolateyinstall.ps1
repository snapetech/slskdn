$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.180/slskdn-main-win-x64.zip"
$checksum   = "d8203e007aff99a243873c88684fcef9e6fcee77f8901b94fcc85e2c0a0d071f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
