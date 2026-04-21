$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.170/slskdn-main-win-x64.zip"
$checksum   = "a234c81ab3640d6d7af12d8a932bdcc919e04c5cb93ab9becc80dae80efd906d"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
