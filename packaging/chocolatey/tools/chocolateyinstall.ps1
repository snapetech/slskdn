$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.174/slskdn-main-win-x64.zip"
$checksum   = "e792266d224bbe6f23cf495380c77d6acb13a201123c1b011217aba356b60bb6"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
