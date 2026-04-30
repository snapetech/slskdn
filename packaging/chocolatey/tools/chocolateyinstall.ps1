$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.209/slskdn-main-win-x64.zip"
$checksum   = "fd9badf93f7325ce5eab5c313b4baa44f9c15235b95931694664d9f6a6852703"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
