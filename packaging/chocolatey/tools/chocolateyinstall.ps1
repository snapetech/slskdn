$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.154/slskdn-main-win-x64.zip"
$checksum   = "d15039ce5522eb631549e50f25c980d56d2b6b514ba0ed10b049460da420e5db"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
