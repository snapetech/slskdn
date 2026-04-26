$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.2/slskdn-main-win-x64.zip"
$checksum   = "c474174c0766144b221c08c51a8b563d0720146ed9fbdf3d813ec8e9c06df118"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
