$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.156/slskdn-main-win-x64.zip"
$checksum   = "ceb58285d202000e142edb7b4a66ff8e69bab9ae45537d838668b02fcaa702bc"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
