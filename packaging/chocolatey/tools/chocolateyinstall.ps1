$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.184/slskdn-main-win-x64.zip"
$checksum   = "a2a1db416dae84ec5f26db5086d616c4077ab9c724ba6808763e736266c45256"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
