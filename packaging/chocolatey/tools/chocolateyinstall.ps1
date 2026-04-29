$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026042900-slskdn.197/slskdn-main-win-x64.zip"
$checksum   = "b310da41093a7289d019d8380cd39e2d324627f1cb9b5c64a80a1931e67b6494"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
