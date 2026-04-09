$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.123/slskdn-main-win-x64.zip"
$checksum   = "0ae249b5df6fbe7bde9389560de8b019da3d38e8f5efe7e68e43783fb52d6df1"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
