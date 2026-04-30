$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.202/slskdn-main-win-x64.zip"
$checksum   = "ef7ebcf3d0d292f8d10bc98cd1595b879cec5885608f8239e11bfcc178fd9be0"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
