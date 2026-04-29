$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.189/slskdn-main-win-x64.zip"
$checksum   = "312b9db04e99ec4e55815f6a948fcab9533ce06c45ba6910f3bc462542f679c2"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
