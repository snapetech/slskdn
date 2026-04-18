$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.152/slskdn-main-win-x64.zip"
$checksum   = "d2108891e07f6b5876fbe97a5166c2a38f32c68fdf8242d066f33841061b702f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
