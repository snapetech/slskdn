$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.169/slskdn-main-win-x64.zip"
$checksum   = "496485683b772082e8cef207ced7174d8f0ab98c06c185e11a3d657765ffa16d"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
