$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026050100-slskdn.216/slskdn-main-win-x64.zip"
$checksum   = "2b837f4f3306ad293a6c5c7be8b87d2a3353f65163f10cd17a3caefb5c6f058a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
