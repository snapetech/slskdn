$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.109/slskdn-main-win-x64.zip"
$checksum   = "7052e2fd99fde0590968227a91cd0f46b67fbfe3ccdda45ca25369758799ebe3"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
