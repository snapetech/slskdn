$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.157/slskdn-main-win-x64.zip"
$checksum   = "81e62dc69b7bc0bea8ec8637f083ad1f3adc58576e701c790f0b8d82d09944e7"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
