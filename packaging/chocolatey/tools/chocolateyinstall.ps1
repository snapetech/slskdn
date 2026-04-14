$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.128/slskdn-main-win-x64.zip"
$checksum   = "a8241afd8b2f4b14028bb5058481e2eab5665b998c13030c982279b1ea34ba92"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
