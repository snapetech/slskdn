$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.184/slskdn-main-win-x64.zip"
$checksum   = "8807d84d2ee96304748b1af5a3c2a21c868b5c5db89a5a977ecc6757b3e1588f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
