$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.137/slskdn-main-win-x64.zip"
$checksum   = "df441cba38136ff4198f698d9459ebf2dddee9aff95e1b59677d635901e54b21"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
