$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.162/slskdn-main-win-x64.zip"
$checksum   = "848f8adf3f4fe1684f9c51eb822c277b38f7ffc04330661475c0221005ea0ebe"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
