$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.136/slskdn-main-win-x64.zip"
$checksum   = "9fba78f2059ef8d65d2c9a38f12a684eef59e9559f69f31d85265b7f49f0d4ec"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
