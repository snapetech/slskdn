$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.148/slskdn-main-win-x64.zip"
$checksum   = "c6bd33766cb3df50ce8e0995f9dbbd47a5bca0c084e38cf5521bbe53a72c8979"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
