$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.129/slskdn-main-win-x64.zip"
$checksum   = "522606e5c9833362ce6aff29e7ddf3b81624f0be2a2cb81acf5c50f983efb11a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
