$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.205/slskdn-main-win-x64.zip"
$checksum   = "4d695a93fbe697fd407723296af7ddde6c4d26d8e2a1e22b4ad064cbab904ebb"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
