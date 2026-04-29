$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.195/slskdn-main-win-x64.zip"
$checksum   = "fda66092ecd8621371f6c891098634647fb780744634dca073b6fa572d261302"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
