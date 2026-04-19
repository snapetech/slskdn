$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.153/slskdn-main-win-x64.zip"
$checksum   = "ff06602d878f5a959d93fd8d0eac5995c8e20651d3d86f6bd2fb2258003f080a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
