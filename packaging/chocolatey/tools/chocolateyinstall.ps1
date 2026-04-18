$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.144/slskdn-main-win-x64.zip"
$checksum   = "39447b0c38e3b5e39d3c3836616f0124018ad56a28d92b9704899687748cb5bc"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
