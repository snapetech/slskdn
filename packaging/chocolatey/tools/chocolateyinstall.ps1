$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.141/slskdn-main-win-x64.zip"
$checksum   = "1d83ab090b1dff57a1087fe8fe0f3ec00660ca6ead1ef099e08aa301ce1c7da7"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
