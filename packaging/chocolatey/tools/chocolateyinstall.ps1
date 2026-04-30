$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.204/slskdn-main-win-x64.zip"
$checksum   = "396b5516311b096bb1cf7527195dfb8637e360c1b28be0f389e0fb90434e95d4"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
