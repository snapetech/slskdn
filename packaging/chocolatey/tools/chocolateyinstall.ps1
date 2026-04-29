$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.190/slskdn-main-win-x64.zip"
$checksum   = "044ca8cde647f0c37f691ce9e2bfd04d978bbeddd8919c30102ed0238f0ac0e6"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
