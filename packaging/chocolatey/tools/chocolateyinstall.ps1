$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.188/slskdn-main-win-x64.zip"
$checksum   = "2e2c908a9e48f3678a84a8398d218ed120921b19e7bd0790f1d46e9d7dfeda19"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
