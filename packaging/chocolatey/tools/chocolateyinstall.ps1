$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.116/slskdn-main-win-x64.zip"
$checksum   = "dd7e9ce45ebbb850069e3269f7280d7aac27de0090a25dd856f856397af05459"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
