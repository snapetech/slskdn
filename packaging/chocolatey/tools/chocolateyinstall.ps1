$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/2026043000-slskdn.208/slskdn-main-win-x64.zip"
$checksum   = "7de807a489a6ef69d4f2a7160e5417559378003ca7caf93eef36cd88a4bcab75"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
