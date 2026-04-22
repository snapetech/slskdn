$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.175/slskdn-main-win-x64.zip"
$checksum   = "6cb319a31e8ee6e6d11e08c4b1ad7622c2862ace7ee596e64bf51e843e274bdf"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
