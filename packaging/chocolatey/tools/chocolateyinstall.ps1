$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.164/slskdn-main-win-x64.zip"
$checksum   = "166b8b4e4fda4908d812a57457626841099c3610358dc60ffa8229b5331ae36a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
