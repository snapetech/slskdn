$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.139/slskdn-main-win-x64.zip"
$checksum   = "af0f514325f093da2874cb604f3b8898cb21958d1efd5cdfba82c08aced354d1"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
