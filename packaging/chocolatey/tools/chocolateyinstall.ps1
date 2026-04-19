$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.159/slskdn-main-win-x64.zip"
$checksum   = "b636dcd86ab4096cb204cbbd89ad425410bbebf71ec5245d97d7245ed0efc9b1"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
