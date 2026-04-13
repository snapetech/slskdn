$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.126/slskdn-main-win-x64.zip"
$checksum   = "740e0baa60425ce498191ab0bb0ffd0d09f8118ac19ef1b1a8ba687e16a2ad8f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
