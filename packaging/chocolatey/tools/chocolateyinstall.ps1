$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.155/slskdn-main-win-x64.zip"
$checksum   = "c5cfc87c55db59775d6a00b195f64a3660a15cfeed4290ff1461c7dc99ce884e"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
