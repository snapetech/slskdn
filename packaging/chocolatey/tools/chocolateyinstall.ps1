$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.113/slskdn-main-win-x64.zip"
$checksum   = "f83c9caf9de5ad16e1cf30388d65f1ce2fc8068f3a29cb77083426bda5decbbc"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
