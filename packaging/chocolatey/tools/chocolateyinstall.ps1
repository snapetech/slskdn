$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.167/slskdn-main-win-x64.zip"
$checksum   = "5f6114f85a9aad4f6f1ab132c53666dfb4ee87c9d98c2fd014b948077106e6c3"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
