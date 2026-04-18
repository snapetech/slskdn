$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.140/slskdn-main-win-x64.zip"
$checksum   = "9fbe70e214c4af4ba5a45bf807952bc804ab90e9a6fab7854f28eee28ffed9e2"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
