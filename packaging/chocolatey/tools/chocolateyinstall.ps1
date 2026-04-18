$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.142/slskdn-main-win-x64.zip"
$checksum   = "7a2776fa476bb2062ec6386ffe61b1fceb8bbb7bf4792f904a7f60588aa87696"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
