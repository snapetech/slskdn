$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.176/slskdn-main-win-x64.zip"
$checksum   = "a5fe66fba4a2ff7f8a23a680ae015f04355f261f492e3b76c0b25161e3a74a94"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
