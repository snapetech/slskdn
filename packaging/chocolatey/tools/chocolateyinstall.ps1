$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.151/slskdn-main-win-x64.zip"
$checksum   = "1b1f80dad3333f62015f366d008164aa25e21d0848df9b2e312f546f6ebb659e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
