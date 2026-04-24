$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.178/slskdn-main-win-x64.zip"
$checksum   = "28dafde05a865b57d47eb5e851df8e6dc4aa4f0f5f1103ee1910314123113f0f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
